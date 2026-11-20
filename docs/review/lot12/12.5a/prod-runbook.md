# Runbook Beta PROD (lot 12, 12.5a, mis à jour après le déploiement du 10/11/2026)

But : mettre en ligne l'API de la Beta PROD sur `https://api.mentorapp.fr`, sur le même VPS que api-demo, avec une base neuve. Aucune valeur de secret n'est écrite ici : chaque secret est généré sur le serveur et écrit directement dans `~/mentora/.env`, sans jamais être affiché.

Ce mode d'emploi décrit ce qui a réellement été fait le 10/11/2026 (preuves : `docs/review/lot12/12.5a/vps-evidence.md`). Il sert aussi pour un redéploiement.

Légende : **[PS]** = terminal PowerShell de VS Code sur le poste, à la racine du dépôt. **[SSH]** = session SSH sur le VPS (`ssh mentora@87.106.108.91`), dans `~/mentora`.

Noms réels sur le VPS : conteneurs `mentora-postgres`, `mentora-api` (service `api`, api-demo), `mentora-api-prod` (service `api-prod`), `mentora-nginx`, `mentora-livekit` ; réseau `mentora-network` ; hôte PostgreSQL `postgres`. `~/mentora` n'est pas un clone git : les fichiers du dépôt s'y envoient par `scp`.

Règles pendant tout le runbook : ne jamais faire `cat ~/mentora/.env` ni `docker compose config` sans filtre (il affiche les valeurs), ne jamais coller un secret ni un code de connexion dans un chat, un ticket ou un message. Toujours `--no-deps` sur `docker compose up` : sans lui, une commande visant un service peut recréer api-demo ou la base.

Ordre important : certificat, puis démarrage de `api-prod`, puis activation de `mentora-prod.conf`. nginx résout le nom `api-prod` au chargement de sa configuration : si le conteneur ne tourne pas, `nginx -t` échoue.

---

## 1. Vérifier le DNS **[PS]**

```powershell
Resolve-DnsName api.mentorapp.fr -Type A; Resolve-DnsName admin.mentorapp.fr -Type A
```

Attendu : les deux noms répondent `87.106.108.91`. Sinon, ne pas continuer (le certificat échouerait). Un « nom DNS n'existe pas » juste après la création de l'enregistrement peut venir du cache local : relancer quelques minutes plus tard.

## 2. Sauvegarder la base api-demo **[SSH]**

Toujours avant de toucher à quoi que ce soit.

```bash
cd ~/mentora && docker exec mentora-postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/backup-$(date +%Y%m%d-%H%M).dump && ls -lht ~/backup-*.dump | head -3
```

Attendu : un fichier `.dump` de taille cohérente avec le précédent.

## 3. Construire l'image et l'envoyer

Étiquette dédiée `mentora-api:lot12` : api-demo garde `mentora-api:latest` tant qu'on ne le bascule pas volontairement.

**[PS]**

```powershell
git --no-optional-locks status --short
git --no-optional-locks log --oneline -1
Get-Content .dockerignore | Select-String "docs"
docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot12 .
docker save -o mentora-api-lot12.tar mentora-api:lot12
scp mentora-api-lot12.tar mentora@87.106.108.91:~/
scp nginx/conf.d/mentora-prod.conf.example nginx/conf.d/mentora-admin.conf.example mentora@87.106.108.91:~/mentora/nginx/conf.d/
Remove-Item mentora-api-lot12.tar; Test-Path mentora-api-lot12.tar
```

Attendu : arbre propre, dernier commit de la branche, `docs/review` dans `.dockerignore`, puis `False`. Les deux fichiers `.example` restent inactifs sur le VPS tant qu'ils gardent ce suffixe (nginx ne charge que `*.conf`).

**[SSH]**

```bash
docker load -i ~/mentora-api-lot12.tar && docker images mentora-api --format '{{.Repository}}:{{.Tag}}\t{{.ID}}\t{{.CreatedAt}}'
rm ~/mentora-api-lot12.tar
```

## 4. Base, rôle et secrets PROD **[SSH]**

Sauvegarde du `.env`, puis retour à la ligne final s'il manque (sinon la ligne ajoutée se collerait à la dernière) :

```bash
cd ~/mentora && cp .env .env.bak-$(date +%Y%m%d-%H%M%S) && ls -l .env*
[ -n "$(tail -c1 .env)" ] && echo >> .env; echo ok
```

Mot de passe de la base, généré dans le `.env`, puis rôle et base créés sans afficher le mot de passe. La base n'accepte pas les autres comptes (REVOKE CONNECT) :

```bash
grep -q '^POSTGRES_PROD_PASSWORD=' .env && echo "DEJA PRESENT" || echo "POSTGRES_PROD_PASSWORD=$(openssl rand -hex 24)" >> .env
PW=$(grep '^POSTGRES_PROD_PASSWORD=' .env | cut -d= -f2-); docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1' <<SQL
CREATE ROLE mentora_prod_user LOGIN PASSWORD '$PW';
CREATE DATABASE mentora_prod OWNER mentora_prod_user;
REVOKE CONNECT ON DATABASE mentora_prod FROM PUBLIC;
SQL
unset PW
```

Attendu : `CREATE ROLE`, `CREATE DATABASE`, `REVOKE`. Si le rôle ou la base existe déjà, s'arrêter et comprendre avant de relancer (ne jamais supprimer sans sauvegarde).

Secrets aléatoires (hexadécimal, 64 caractères, tous différents) et liste des comptes admin :

```bash
for n in JWT_SECRET_PROD ADMIN_JWT_SECRET_PROD ADMIN_JWT_SECRET_DEMO; do grep -q "^$n=" .env && echo "$n DEJA PRESENT" || echo "$n=$(openssl rand -hex 32)" >> .env; done
grep -q '^ADMIN_EMAILS=' .env && echo "DEJA PRESENT" || echo "ADMIN_EMAILS=<les 4 boîtes mentorapp.fr des associés, séparées par des virgules, sans espace>" >> .env
for n in POSTGRES_PROD_PASSWORD JWT_SECRET_PROD ADMIN_JWT_SECRET_PROD ADMIN_JWT_SECRET_DEMO ADMIN_EMAILS; do grep -q "^$n=" .env && echo "$n present" || echo "$n MANQUANT"; done
```

Les réglages SMTP (hors mot de passe), LiveKit (hors clés) et Quiz viennent du `appsettings.json` commun à tous les environnements (`appsettings.Development.json` n'est pas dans l'image). `api-prod` reprend donc les mêmes variables que `api` : `EMAIL_SMTP_PASSWORD`, `EMAIL_ROUTING_TEST_RECIPIENTS`, `LIVEKIT_API_KEY`, `LIVEKIT_API_SECRET`.

## 5. Ajouter le service `api-prod` à `docker-compose.yml` **[SSH]**

```bash
cp docker-compose.yml docker-compose.yml.pre-lot12 && nano docker-compose.yml
```

Coller ce bloc entre le service `api` et `nginx:` (deux espaces devant chaque ligne). Dans nano : Ctrl+O, puis **Entrée** pour confirmer le nom du fichier, puis Ctrl+X. Si VS Code intercepte une touche : F3 pour enregistrer, F2 pour quitter.

```yaml
  api-prod:
    image: mentora-api:lot12
    container_name: mentora-api-prod
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=mentora_prod;Username=mentora_prod_user;Password=${POSTGRES_PROD_PASSWORD}
      - Jwt__Secret=${JWT_SECRET_PROD}
      - AdminAuth__JwtSecret=${ADMIN_JWT_SECRET_PROD}
      - AdminAuth__Emails=${ADMIN_EMAILS}
      - Cors__AllowedOrigins=https://admin.mentorapp.fr
      - Email__Smtp__Password=${EMAIL_SMTP_PASSWORD}
      - EmailRouting__TestRecipients=${EMAIL_ROUTING_TEST_RECIPIENTS}
      - LiveKit__ApiKey=${LIVEKIT_API_KEY}
      - LiveKit__ApiSecret=${LIVEKIT_API_SECRET}
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - mentora-network
```

Pas de `ports:` : seul nginx parle à ce conteneur. Pas de `EmailRouting__Mode` : mode TEST jusqu'au jour de la Bêta. Fenêtre visio par défaut (15 minutes avant, 15 minutes après).

```bash
docker compose config --quiet && echo "compose valide"; docker compose config --services | grep api-prod
```

L'API refuse de démarrer en Production si `Jwt__Secret` ou `AdminAuth__JwtSecret` ne vient pas d'une variable d'environnement d'au moins 32 caractères, si `ConnectionStrings__DefaultConnection` manque, si `Cors__AllowedOrigins` est vide, en `http://` ou avec un joker, ou si le secret admin est celui de l'app. Dans ces cas elle s'arrête avant les migrations.

## 6. Certificat **[SSH]**

certbot est installé sur le VPS lui-même (pas dans un conteneur) ; le dossier `/var/www/certbot` est monté dans nginx.

6.1 Bloc HTTP provisoire pour les deux noms :

```bash
cat > nginx/conf.d/mentora-prod-http.conf <<'CONF'
server {
    listen 80;
    server_name api.mentorapp.fr admin.mentorapp.fr;
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
        default_type "text/plain";
    }
    location / {
        return 404;
    }
}
CONF
docker compose exec nginx nginx -t && docker compose exec nginx nginx -s reload
```

6.2 Un certificat pour les deux noms (mot de passe sudo demandé) :

```bash
sudo certbot certonly --webroot -w /var/www/certbot -d api.mentorapp.fr -d admin.mentorapp.fr --cert-name api.mentorapp.fr
```

Attendu : `Successfully received certificate`, fichiers sous `/etc/letsencrypt/live/api.mentorapp.fr/`.

6.3 Rechargement de nginx après chaque renouvellement (le renouvellement automatique est fait par le minuteur `snap.certbot.renew.timer` ; sans ce hook, nginx garderait l'ancien certificat en mémoire) :

```bash
printf '#!/bin/sh\ndocker exec mentora-nginx nginx -s reload\n' | sudo tee /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh >/dev/null && sudo chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh && sudo ls -l /etc/letsencrypt/renewal-hooks/deploy/
```

## 7. Démarrer api-prod et contrôler

**[SSH]**

```bash
docker compose up -d --no-deps api-prod && sleep 25 && docker logs mentora-api-prod 2>&1 | grep -c "Applying migration"
docker logs mentora-api-prod 2>&1 | grep -E 'Seeder|seeded|\[EMAIL\] routing|\[ADMIN\]|\[CORS\]|\[PATCH\]|Application started|Now listening|fail:|crit:|Exception' | grep -v -E 'OTP|EMAIL-LOG'
docker ps --filter name=mentora-api-prod --format '{{.Names}}\t{{.Status}}\t{{.Ports}}'
```

Attendu : toutes les migrations sur une base neuve, `[EMAIL] routing mode=TEST`, `[CORS] origins=1 localhost=False`, `[CORS] origin https://admin.mentorapp.fr`, `[ADMIN] accounts listed=4 created=4`, seeders et patchs, `Application started`, aucune ligne `fail:` ni `crit:`, et le port `8080/tcp` seul (non publié). Ne jamais afficher les lignes contenant le marqueur OTP ou le marqueur d'envoi d'email.

Puis, api-prod tournant, la conf nginx définitive de l'API :

```bash
rm nginx/conf.d/mentora-prod-http.conf && cp nginx/conf.d/mentora-prod.conf.example nginx/conf.d/mentora-prod.conf && docker compose exec nginx nginx -t && docker compose exec nginx nginx -s reload
```

Si `nginx -t` échoue : `rm nginx/conf.d/mentora-prod.conf`, puis comprendre avant toute autre action.

**[PS]**

```powershell
(Invoke-WebRequest https://api.mentorapp.fr/api/v1/health -UseBasicParsing).StatusCode
try { Invoke-WebRequest https://api.mentorapp.fr/api/v1/admin/exercises -UseBasicParsing } catch { [int]$_.Exception.Response.StatusCode }
$r = Invoke-WebRequest https://api.mentorapp.fr/api/v1/admin/auth/otp/request -Method Options -UseBasicParsing -Headers @{ Origin = "https://admin.mentorapp.fr"; "Access-Control-Request-Method" = "POST"; "Access-Control-Request-Headers" = "content-type" }; "$($r.StatusCode) $($r.Headers['Access-Control-Allow-Origin'])"
```

Attendu : `200`, `401`, `204 https://admin.mentorapp.fr`.

## 8. Retour arrière de la PROD **[SSH]**

À faire dans cet ordre, en s'arrêtant dès que le problème est réglé :

```bash
cd ~/mentora
docker compose stop api-prod
docker compose rm -f api-prod
rm -f nginx/conf.d/mentora-prod.conf nginx/conf.d/mentora-prod-http.conf
docker compose exec nginx nginx -t && docker compose exec nginx nginx -s reload
cp docker-compose.yml.pre-lot12 docker-compose.yml
```

api-demo n'est jamais touché par ce retour arrière. La base `mentora_prod` peut rester : elle ne sert qu'à api-prod.

---

## Pour api-demo : déployer le code du lot 12

**A. Doublons de noms d'exercices Mentora (lecture seule) [SSH].** Le lot 12 ajoute un index unique sur le nom (insensible à la casse et aux espaces de bord) pour les exercices Mentora (sans coach). Si un doublon existe, la migration échouerait au démarrage.

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
BEGIN READ ONLY;
SELECT lower(btrim("EXERCISE_NAME")) AS nom, count(*) AS nb
FROM "EXERCISES"
WHERE "EXERCISE_COACH_ID" IS NULL
GROUP BY 1
HAVING count(*) > 1;
ROLLBACK;
SQL
```

Attendu : `(0 rows)`. Sinon, ne pas déployer : traiter les doublons d'abord.

Seconde requête, lecture seule aussi, qui ignore en plus les accents (même table de caractères que `AccentFolding` dans `Mentora.Core/Text`). L'index de la base ne voit pas ces doublons, mais l'admin les refuse : sur deux exercices qui ne diffèrent que par les accents, toute modification de l'un ou de l'autre répond 409 `EXERCISE_NAME_TAKEN`.

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
BEGIN READ ONLY;
SELECT lower(btrim(translate("EXERCISE_NAME",
         'àâäÀÂÄéèêëÉÈÊËîïÎÏôöÔÖùûüÙÛÜÿŸçÇ',
         'aaaAAAeeeeEEEEiiIIooOOuuuUUUyYcC'))) AS nom, count(*) AS nb
FROM "EXERCISES"
WHERE "EXERCISE_COACH_ID" IS NULL
GROUP BY 1
HAVING count(*) > 1;
ROLLBACK;
SQL
```

Attendu : `(0 rows)`. Si une ligne sort, la migration passe quand même, mais il faut renommer l'un des deux exercices sur api-demo avant de déployer, avec François.

**B. Variables d'administration pour api-demo [SSH].** La sauvegarde `pg_dump` de l'étape 2 doit être récente. Dans le service `api` du compose, sous la dernière ligne `LiveKit__...`, ajouter (six espaces devant) :

```bash
cp docker-compose.yml docker-compose.yml.pre-demo-lot12 && nano docker-compose.yml
```

```yaml
      - AdminAuth__JwtSecret=${ADMIN_JWT_SECRET_DEMO}
      - AdminAuth__Emails=${ADMIN_EMAILS}
```

Sans elles, l'API refuse de démarrer.

**C. Bascule d'api-demo [SSH].** L'ancienne image est gardée sous `pre-lot12` :

```bash
docker compose config --quiet && echo "compose valide" && docker tag mentora-api:latest mentora-api:pre-lot12 && docker tag mentora-api:lot12 mentora-api:latest && docker compose up -d --no-deps --force-recreate api && sleep 25 && docker logs mentora-api 2>&1 | grep -c "Applying migration"
docker logs mentora-api 2>&1 | grep -E 'Seeder|\[EMAIL\] routing|\[ADMIN\]|\[CORS\]|\[PATCH\].*applied|\[PATCH\].*skipped|Application started|fail:|crit:|Exception' | grep -v -E 'OTP|EMAIL-LOG'
```

Attendu : `3` migrations, `[ADMIN] accounts listed=4 created=4`, `[CORS] origins=0 localhost=True` (Development : seules les origines localhost), les deux patchs `applied`, seeders en `existing`, aucune ligne `fail:` ni `crit:`.

**[PS]**

```powershell
(Invoke-WebRequest https://api-demo.mentorapp.fr/api/v1/health -UseBasicParsing).StatusCode; try { Invoke-WebRequest https://api-demo.mentorapp.fr/api/v1/admin/exercises -UseBasicParsing } catch { [int]$_.Exception.Response.StatusCode }
```

Attendu : `200` puis `401`.

**D. Retour arrière d'api-demo [SSH]** (seulement en cas de problème) :

```bash
cp docker-compose.yml.pre-demo-lot12 docker-compose.yml && docker tag mentora-api:pre-lot12 mentora-api:latest && docker compose up -d --no-deps --force-recreate api
```

Les migrations du lot 12 restent alors dans la base : pour revenir à l'état d'avant, restaurer la sauvegarde de l'étape 2 avec `pg_restore` (avec François, après avoir arrêté `api`).

---

## Admin (12.5b)

But : servir l'admin (Blazor WebAssembly, fichiers statiques) sur `https://admin.mentorapp.fr`, avec le conteneur nginx qui tourne déjà. À faire après le runbook ci-dessus (le certificat de l'étape 6 couvre déjà `admin.mentorapp.fr`, et api-prod répond).

Légende : **[PS]** = terminal PowerShell de VS Code sur ton poste, à la racine du dépôt. **[SSH]** = session SSH sur le VPS.

### A. Publier l'admin **[PS]**

```powershell
if (Test-Path docs/review/lot12/12.5b/publish) { Remove-Item -Recurse -Force docs/review/lot12/12.5b/publish }; dotnet publish Mentora.Admin -c Release -o docs/review/lot12/12.5b/publish
```

Le dossier `publish/` est ignoré par git ; il est vidé avant chaque publication pour ne jamais envoyer d'ancien fichier. Le résultat utile est le sous-dossier `wwwroot`.

### B. Contrôler les fichiers de configuration publiés **[PS]**

```powershell
Get-ChildItem docs/review/lot12/12.5b/publish/wwwroot -Filter 'appsettings*.json' | ForEach-Object Name
Get-Content docs/review/lot12/12.5b/publish/wwwroot/appsettings.Production.json
```

Attendu : exactement deux fichiers, `appsettings.json` et `appsettings.Production.json` (jamais `appsettings.Development.json`), et `ApiBaseUrl` vaut `https://api.mentorapp.fr`. Sinon, ne pas continuer.

### C. Archiver le dossier à envoyer **[PS]**

```powershell
tar -czf docs/review/lot12/12.5b/publish/admin-www.tgz -C docs/review/lot12/12.5b/publish/wwwroot .
```

L'archive est créée dans `publish/`, le dossier ignoré par git, pour ne jamais être commitée par erreur.

### D. Envoyer l'archive sur le VPS **[PS]**

```powershell
scp docs/review/lot12/12.5b/publish/admin-www.tgz mentora@87.106.108.91:~/mentora/admin-www.tgz
```

### E. Installer la nouvelle version, avec retour arrière possible **[SSH]**

La version précédente est conservée dans `admin-www.prev`.

```bash
cd ~/mentora
rm -rf admin-www.prev
[ -d admin-www ] && mv admin-www admin-www.prev
mkdir admin-www
tar -xzf admin-www.tgz -C admin-www
ls admin-www | head
rm admin-www.tgz
```

Attendu : on voit `index.html`, `_framework`, `css`, `js`, `img`, `appsettings.json`, `appsettings.Production.json`.

**Obligatoire si nginx tournait déjà** (constaté le 12/11) : le volume `./admin-www:/var/www/mentora-admin:ro` suit le dossier par son identité sur le disque, pas par son nom. Après `mv admin-www admin-www.prev`, nginx continue de servir `admin-www.prev`. Redémarrer nginx (coupure de quelques secondes pour api, api-demo, admin et LiveKit), puis vérifier le fichier réellement vu par nginx :

```bash
docker compose restart nginx && sleep 3 && docker exec mentora-nginx ls -l /var/www/mentora-admin/js/admin.js
```

Le retour arrière K (échange des dossiers) demande le même redémarrage.

### F. Une seule fois : monter le dossier dans le service nginx **[SSH]**

Sauvegarde d'abord :

```bash
cd ~/mentora
cp docker-compose.yml docker-compose.yml.pre-admin
```

Dans `docker-compose.yml` (`nano docker-compose.yml`), sous `volumes:` du service `nginx`, ajouter cette ligne (en lecture seule, six espaces devant) sous `- /var/www/certbot:/var/www/certbot:ro`. Dans nano : Ctrl+O, puis **Entrée** pour confirmer le nom du fichier, puis Ctrl+X :

```yaml
      - ./admin-www:/var/www/mentora-admin:ro
```

Puis :

```bash
docker compose config --quiet && echo "compose valide"
docker compose up -d --no-deps nginx
```

### G. Version de nginx et type MIME du wasm **[SSH]**

```bash
docker exec mentora-nginx nginx -v
docker exec mentora-nginx grep wasm /etc/nginx/mime.types
```

Attendu : une ligne `application/wasm   wasm;`. Remplacer `mentora-nginx` par le nom réel du conteneur nginx (`docker ps`). Si la ligne manque, s'arrêter et le dire : il faudra ajouter une entrée de type dans le fichier de configuration.

### H. Installer la configuration nginx de l'admin **[SSH]**

Le fichier `nginx/conf.d/mentora-admin.conf.example` du dépôt se copie sans le suffixe `.example`. Le dossier `~/mentora` du VPS n'est pas un clone git : le fichier s'envoie depuis le poste.

H.1 Vérifier que le dossier `~/mentora/nginx/conf.d` est bien celui que le service nginx monte **[SSH]** :

```bash
cd ~/mentora
ls ~/mentora/nginx/conf.d
docker compose config | grep -A3 conf.d
```

Attendu : le volume de nginx pointe vers `./nginx/conf.d`. S'il pointe ailleurs, utiliser le dossier monté à la place de `~/mentora/nginx/conf.d` dans les commandes suivantes.

H.2 Envoyer le fichier depuis le poste **[PS]** :

```powershell
scp nginx/conf.d/mentora-admin.conf.example mentora@87.106.108.91:~/mentora/nginx/conf.d/mentora-admin.conf.example
```

H.3 Activer et tester **[SSH]** :

```bash
cd ~/mentora
cp nginx/conf.d/mentora-admin.conf.example nginx/conf.d/mentora-admin.conf
docker compose exec nginx nginx -t
```

Attendu : `syntax is ok` et `test is successful`. Si `nginx -t` échoue, retirer `mentora-admin.conf` avant toute autre action :

```bash
rm nginx/conf.d/mentora-admin.conf
```

Puis recharger :

```bash
docker compose exec nginx nginx -s reload
```

Il n'y a qu'un seul fichier à copier : les en-têtes de sécurité sont déclarés directement dans le bloc serveur, il n'y a pas de fichier d'en-têtes séparé.

### I. Contrôles avec curl **[PS]**

```powershell
curl.exe -I https://admin.mentorapp.fr/
curl.exe -I https://admin.mentorapp.fr/exercices
curl.exe -I https://admin.mentorapp.fr/_framework/dotnet.native.wasm
curl.exe -I https://admin.mentorapp.fr/appsettings.Development.json
curl.exe -I http://admin.mentorapp.fr/
```

Attendu :
- `/` : `200`, `content-type: text/html`, et les en-têtes `strict-transport-security`, `x-content-type-options`, `x-frame-options`, `referrer-policy`, `permissions-policy`, `content-security-policy`, `cache-control: no-cache`.
- `/exercices` : `200` (retour sur `index.html`, c'est normal pour une application à page unique).
- le fichier `.wasm` : `200` et `content-type: application/wasm`. Si le nom du fichier diffère, lister `~/mentora/admin-www/_framework` côté VPS et en prendre un.
- `appsettings.Development.json` : ne doit pas servir de configuration locale (le fichier n'existe pas dans le dossier publié, la réponse est un `200` de repli sur `index.html` sans contenu JSON, ou un `404`). Vérifier que le corps n'est pas du JSON : `curl.exe -s https://admin.mentorapp.fr/appsettings.Development.json | Select-String localhost` ne doit rien afficher.
- `http://` : `301` vers `https://admin.mentorapp.fr/`.

### J. Test dans le navigateur

Fait par Claude avec Claude in Chrome, dans son propre onglet : page de connexion, console et Content-Security-Policy, demande d'un code. François lit le code dans sa boîte mail (en mode TEST, le code part aux 4 associés) et le tape lui-même dans l'onglet, jamais dans un chat. Puis : liste et recherche des exercices, ouverture d'une fiche sans enregistrer, session après rechargement, déconnexion.

### K. Retour arrière de l'admin **[SSH]**

Version précédente des fichiers :

```bash
cd ~/mentora
rm -rf admin-www.broken
mv admin-www admin-www.broken
mv admin-www.prev admin-www
```

Retirer complètement l'admin :

```bash
cd ~/mentora
rm -f nginx/conf.d/mentora-admin.conf
docker compose exec nginx nginx -t && docker compose exec nginx nginx -s reload
cp docker-compose.yml.pre-admin docker-compose.yml
docker compose up -d --no-deps nginx
```

`docker-compose.yml.pre-admin` est la sauvegarde de l'étape F. Ce retour arrière ne touche ni api-prod ni api-demo.

---

## Vague 2 du lot 12 : vidéos MUX (déployée le 12/11/2026)

Preuves : `docs/review/lot12/wave2-vps/vps-evidence.md`. Même déroulé que ci-dessus (sauvegardes, image construite sur le poste, `docker load`, api-demo puis api-prod, admin puis conf nginx), avec ces ajouts.

### Environnements MUX

Organisation MUX « tfoinm » : environnement « Mentora PROD » pour api-prod, « Mentora DEMO » pour api-demo. Dans chacun : un jeton d'accès (Settings > Access Tokens, droits Mux Video lecture et écriture) et un webhook (Settings > Webhooks) vers `https://api.mentorapp.fr/api/v1/webhooks/mux` (PROD) ou `https://api-demo.mentorapp.fr/api/v1/webhooks/mux` (DEMO). La clé de signature du webhook est la valeur masquée de la colonne SECRET (icône œil), à ne pas confondre avec « Signing Keys ».

### Secrets dans `~/mentora/.env` **[SSH]**

Sauvegarde du `.env` et retour à la ligne final (étape 4), puis saisie masquée. Coller chaque valeur **une seule fois** (dans le terminal de VS Code, Ctrl+V et clic droit collent chacun : la valeur se retrouve en double).

```bash
for n in MUX_TOKEN_ID_PROD MUX_TOKEN_SECRET_PROD MUX_WEBHOOK_SECRET_PROD MUX_TOKEN_ID_DEMO MUX_TOKEN_SECRET_DEMO MUX_WEBHOOK_SECRET_DEMO; do if grep -q "^$n=" .env; then echo "$n DEJA PRESENT"; else read -rs -p "$n : " v; echo; printf '%s=%s\n' "$n" "$v" >> .env; unset v; fi; done
awk -F= '/^MUX_/{v=substr($0,length($1)+2); L=length(v); printf "%s longueur=%d espace=%s dollar=%s double=%s\n",$1,L,(v ~ /[[:space:]]/?"OUI":"non"),(index(v,"$")?"OUI":"non"),((L%2==0 && substr(v,1,L/2)==substr(v,L/2+1))?"OUI":"non")}' .env
```

Attendu : longueurs 36 (Token ID), 75 (Token Secret), 32 (clé de webhook), et `non` partout.

### Compose **[SSH]**

Dans `api-prod` (après `LiveKit__ApiSecret`) :

```yaml
      - Mux__TokenId=${MUX_TOKEN_ID_PROD}
      - Mux__TokenSecret=${MUX_TOKEN_SECRET_PROD}
      - Mux__WebhookSecret=${MUX_WEBHOOK_SECRET_PROD}
      - Mux__CorsOrigin=https://admin.mentorapp.fr
```

Dans `api` (api-demo, après `AdminAuth__Emails`) : les mêmes avec les variables `_DEMO` et `Mux__CorsOrigin=http://localhost:4200` (pas d'admin-demo ; l'admin locale dépose sur MUX DEMO). Hors Development, api-prod refuse de démarrer si l'une des quatre manque, si la clé de webhook contient un espace, ou si l'origine n'est pas une seule origine https.

### Contrôles après démarrage

- Logs : `Applying migration '20261110104601_Lot12_3a_MuxVideos'` au premier démarrage, `[MUX] configured=True corsOrigin=<origine attendue> apiBase=api.mux.com`.
- **[PS]** POST `/api/v1/admin/exercises/00000000-0000-0000-0000-000000000000/video-uploads` sans jeton : 401 ; POST `/api/v1/webhooks/mux` avec `{}` : 401 (un 503 voudrait dire MUX non configuré).
- Conf nginx de l'admin : la CSP servie contient `img-src ... https://image.mux.com` et `connect-src ... https://*.mux.com`.
- Admin servie : `admin.js` contient `HTTP_308_STALLED` (sinon, voir l'étape E : redémarrer nginx).

### Retour arrière

- api-demo : `docker tag mentora-api:pre-lot12-v2 mentora-api:latest && docker compose up -d --no-deps --force-recreate api`.
- api-prod : `cp docker-compose.yml.pre-lot12-v2 docker-compose.yml && docker compose up -d --no-deps --force-recreate api-prod` (image `mentora-api:lot12`).
- Admin : étape K, puis `cp ~/mentora-admin.conf.pre-v2 nginx/conf.d/mentora-admin.conf`, `nginx -t`, reload, et redémarrage de nginx.
- La migration 12.3a reste dans les bases (colonnes et table ajoutées, sans effet sur l'ancien code).

---

## Lot 13.1 : origine de débogage (Cors__DebugOrigins)

Permet à l'app mobile lancée en local (navigateur sur `http://localhost:8100`) d'appeler l'API de PROD. Réglage séparé de `Cors__AllowedOrigins`, variable d'environnement uniquement, mêmes règles dans tous les environnements.

### Compose **[SSH]**

Dans `api-prod`, après `Cors__AllowedOrigins` (six espaces d'indentation, comme les autres lignes) :

```yaml
      - Cors__DebugOrigins=http://localhost:8100
```

Puis `docker compose up -d --no-deps --force-recreate api-prod`.

### Règles de validation (refus au démarrage, avant les migrations)

Chaque élément de `Cors__DebugOrigins` (séparés par des virgules, 5 au plus) doit être :

- une origine absolue en `http` ou `https`, sans joker, sans identifiants, sans chemin, sans requête ni fragment ;
- avec l'hôte exactement `localhost` (`127.0.0.1`, `[::1]`, `sub.localhost` et `localhost.evil.com` sont refusés) ;
- avec un port explicite autre que le port par défaut (`http://localhost` et `http://localhost:80` sont refusés).

Vide ou absent : aucune origine de débogage, démarrage normal. En plus, hors Development, un élément de `Cors__AllowedOrigins` dont l'hôte est `localhost` est refusé : il faut utiliser `Cors__DebugOrigins`. La règle « `https` obligatoire » de `Cors__AllowedOrigins` hors Development ne change pas. Les messages de refus nomment le réglage et le numéro de l'élément, jamais la valeur (par exemple `Cors:DebugOrigins item #1 must use the host localhost.`).

### Contrôles après démarrage

**[SSH]** Attendu dans les logs de PROD :

```
[CORS] origins=1 localhost=False
[CORS] origin https://admin.mentorapp.fr
[CORS] debugOrigins=1
[CORS] debug origin http://localhost:8100
```

**[PS]** Contrôle de la pré-requête depuis l'origine de débogage (même forme que le contrôle de l'admin, étape 7) :

```powershell
$r = Invoke-WebRequest https://api.mentorapp.fr/api/v1/auth/otp/request -Method Options -UseBasicParsing -Headers @{ Origin = "http://localhost:8100"; "Access-Control-Request-Method" = "POST"; "Access-Control-Request-Headers" = "content-type" }; "$($r.StatusCode) $($r.Headers['Access-Control-Allow-Origin'])"
```

Attendu : `204 http://localhost:8100`.

### api-demo

Rien à ajouter : api-demo tourne en Development, qui accepte déjà `http://localhost:8100`. `[CORS] debugOrigins=0` y est normal.

### Retour arrière

Retirer la ligne `Cors__DebugOrigins` du compose et recréer `api-prod` : plus aucune origine localhost n'est acceptée.

---

## Lot 13 : messages vocaux (stockage audio, taille des requêtes, origine de débogage)

But : déployer le lot 13 (13.1 à 13.4) sur api-demo puis sur api-prod. Le code exige `Audio__StoragePath`, un dossier absolu, existant et accessible en écriture. Hors Development (api-prod), l'API refuse de démarrer sans lui. En Development (api-demo), elle démarre avec `[AUDIO] configured=False` et chaque envoi ou écoute répond 503 `AUDIO_NOT_CONFIGURED`. Le dossier doit être un dossier de l'hôte monté dans le conteneur : sinon les fichiers sont perdus au prochain `--force-recreate` et les messages restent sans fichier (404 à l'écoute).

Étiquette de l'image : `mentora-api:lot13`. Aucune valeur de secret dans cette section. Ne jamais afficher les lignes de log contenant le marqueur OTP ou le marqueur d'envoi d'email.

### 1. Prérequis : place disque, image compressée, chargement

1.1 Place disque sur le VPS **[SSH]**, avant d'envoyer l'image :

```bash
df -h /
```

Attendu : au moins 3 Go libres (archive d'environ 0,4 Go, image chargée d'environ 1 Go, marge pour la base et les fichiers audio). Sinon, ne pas continuer.

1.2 Construire, exporter et compresser l'image **[PS]**. `docker save | gzip` n'existe pas dans Windows PowerShell 5.1 : l'image est d'abord exportée en `.tar`, puis compressée avec la classe .NET `GZipStream` (présente dans PowerShell 5.1). L'archive est faite dans `$env:TEMP`, hors du dépôt (synchronisé par OneDrive, et jamais commitée par erreur) :

```powershell
git --no-optional-locks status --short
git --no-optional-locks log --oneline -1
docker build --platform linux/amd64 -f Mentora.API/Dockerfile -t mentora-api:lot13 .
docker save -o "$env:TEMP\mentora-api-lot13.tar" mentora-api:lot13
$t = "$env:TEMP\mentora-api-lot13.tar"; $i = [IO.File]::OpenRead($t); $o = [IO.File]::Create("$t.gz"); $g = New-Object IO.Compression.GZipStream($o, [IO.Compression.CompressionLevel]::Optimal); $i.CopyTo($g); $g.Dispose(); $o.Dispose(); $i.Dispose()
Remove-Item "$env:TEMP\mentora-api-lot13.tar"; Get-Item "$env:TEMP\mentora-api-lot13.tar.gz" | Select-Object Name, Length
Push-Location $env:TEMP; scp mentora-api-lot13.tar.gz mentora@87.106.108.91:~/; Pop-Location
```

Attendu : arbre propre, dernier commit de la branche, puis une seule archive `.tar.gz` (environ 0,4 Go). Si la compression échoue, envoyer le `.tar` tel quel (plus long à transférer, `docker load` le lit aussi).

1.3 Charger l'image sur le VPS **[SSH]**. `docker load` lit directement une archive compressée en gzip :

```bash
docker load -i ~/mentora-api-lot13.tar.gz && docker images mentora-api --format '{{.Repository}}:{{.Tag}}\t{{.ID}}\t{{.CreatedAt}}'
```

Attendu : `mentora-api:lot13` dans la liste. L'archive est supprimée à l'étape 12, après le déploiement.

### 2. Sauvegardes avant la migration **[SSH]**

Base api-demo (étape 2 du runbook), puis base PROD :

```bash
cd ~/mentora && docker exec mentora-postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/backup-$(date +%Y%m%d-%H%M).dump && ls -lht ~/backup-*.dump | head -3
cd ~/mentora && docker exec mentora-postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d mentora_prod -Fc' > ~/backup-prod-$(date +%Y%m%d-%H%M).dump && ls -lht ~/backup-prod-*.dump | head -3
```

Attendu : deux fichiers `.dump` de taille cohérente avec les précédents.

À partir du deuxième déploiement (les dossiers audio existent alors), sauvegarder aussi chaque dossier audio, en même temps que les `pg_dump` (une base restaurée sans ses fichiers donne des messages vocaux sans fichier) :

```bash
tar czf ~/backup-media-demo-$(date +%Y%m%d-%H%M).tgz -C ~/mentora media-demo && ls -lh ~/backup-media-demo-*.tgz | tail -1
tar czf ~/backup-media-prod-$(date +%Y%m%d-%H%M).tgz -C ~/mentora media-prod && ls -lh ~/backup-media-prod-*.tgz | tail -1
```

Les fichiers créés par le conteneur sont lisibles par tous (644, dossiers 755) : `tar` fonctionne sans `sudo`.

### 3. Contrôle de la base avant la migration (lecture seule) **[SSH]**

La migration `20261117234001_Lot13_2_VoiceMessages` s'applique au démarrage de la nouvelle image. Avant de démarrer, sur api-demo (`mentora_db`, ici `$POSTGRES_DB`) :

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' <<'SQL'
BEGIN READ ONLY;
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3;
SELECT column_name FROM information_schema.columns
 WHERE table_schema = current_schema() AND table_name = 'MESSAGES'
   AND column_name IN ('MESSAGE_TYPE','MESSAGE_AUDIO_PATH','MESSAGE_AUDIO_CONTENT_TYPE',
                       'MESSAGE_AUDIO_DURATION_MS','MESSAGE_AUDIO_SIZE_BYTES','MESSAGE_AUDIO_WAVEFORM');
SELECT conname FROM pg_constraint WHERE conname IN ('CK_MESSAGES_TYPE','CK_MESSAGES_AUDIO_FIELDS');
SELECT count(*) AS total_rows,
       count(*) FILTER (WHERE NOT (v.t IN ('TEXT','AUDIO'))) AS ck_type_violations,
       count(*) FILTER (WHERE NOT (
           (v.t = 'TEXT' AND v.p IS NULL AND v.c IS NULL AND v.d IS NULL AND v.s IS NULL AND v.w IS NULL)
           OR
           (v.t = 'AUDIO' AND m."MESSAGE_CONTENT" = '' AND v.p IS NOT NULL)
       )) AS ck_audio_violations,
       count(*) FILTER (WHERE m."MESSAGE_CONTENT" IS NULL) AS null_content,
       count(*) FILTER (WHERE m."MESSAGE_CONTENT" = '') AS empty_content_info_only
FROM "MESSAGES" m
CROSS JOIN LATERAL (SELECT 'TEXT'::varchar(10) AS t, NULL::varchar AS p, NULL::varchar AS c,
                           NULL::int AS d, NULL::int AS s, NULL::jsonb AS w) v;
SELECT pg_size_pretty(pg_total_relation_size('"MESSAGES"'));
ROLLBACK;
SQL
```

Puis la même commande sur PROD, en remplaçant seulement `-d "$POSTGRES_DB"` par `-d mentora_prod`.

Attendu, sur chaque base :
- première requête : la ligne du haut est `20261110104601_Lot12_3a_MuxVideos` (dernière migration appliquée, lot 13 pas encore appliqué) ;
- deuxième et troisième requêtes : `(0 rows)` ;
- quatrième requête : `ck_type_violations = 0`, `ck_audio_violations = 0`, `null_content = 0` (`empty_content_info_only` est pour information : un contenu vide est accepté pour un message texte) ;
- dernière requête : la taille de la table (la vérification des contraintes pose un verrou exclusif bref, proportionnel à cette taille).

Sinon, ne pas déployer : comprendre d'abord.

### 4. Dossiers audio sur l'hôte **[SSH]**

Un dossier par environnement, jamais partagé :

```bash
mkdir -p ~/mentora/media-demo ~/mentora/media-prod
chmod 750 ~/mentora/media-demo ~/mentora/media-prod && ls -ld ~/mentora/media-demo ~/mentora/media-prod
```

Attendu : `drwxr-x---`, propriétaire `mentora`. Pourquoi 750 : le conteneur de l'API tourne en root (accepté, déjà le cas avant le lot 13), et root écrit dans le dossier quels que soient ses droits ; aucun autre compte de l'hôte n'a à lire les messages vocaux (rien pour « autres ») ; le compte `mentora` garde la lecture pour les sauvegardes `tar`. La preuve d'écriture est la ligne de démarrage `storage=True` (étape 8), qui vient d'un fichier d'essai créé puis supprimé par l'API. Si un jour l'image ajoute `USER app`, il faudra `sudo chown 1654:1654` sur les deux dossiers.

Les sous-dossiers et fichiers créés par le conteneur appartiennent à root : pour supprimer un fichier audio, passer par le conteneur (`docker exec mentora-api rm ...`, voir l'étape 10).

### 5. Compose **[SSH]**

Sauvegarde nommée, puis édition :

```bash
cd ~/mentora && cp docker-compose.yml docker-compose.yml.pre-lot13 && ls -l docker-compose.yml*
docker inspect --format '{{.Config.Image}}' mentora-api-prod
nano docker-compose.yml
```

Noter l'image affichée par `docker inspect` : c'est l'étiquette de retour arrière d'api-prod (étape 11).

Dans le service `api` (api-demo) : sous la dernière ligne de `environment:` (six espaces devant) :

```yaml
      - Audio__StoragePath=/app/media
```

et, au même niveau que `environment:` (quatre espaces devant ; si le service a déjà une clé `volumes:`, ajouter seulement la ligne du dessous) :

```yaml
    volumes:
      - ./media-demo:/app/media
```

Dans le service `api-prod` : remplacer la ligne `image:` par `    image: mentora-api:lot13`, ajouter sous la dernière ligne de `environment:` :

```yaml
      - Audio__StoragePath=/app/media
      - Cors__DebugOrigins=http://localhost:8100
```

(règles de `Cors__DebugOrigins` : section « Lot 13.1 » ci-dessus ; si la ligne y a déjà été ajoutée, ne pas la doubler), et au même niveau que `environment:` :

```yaml
    volumes:
      - ./media-prod:/app/media
```

Dans nano : Ctrl+O, puis **Entrée**, puis Ctrl+X. Puis :

```bash
docker compose config --quiet && echo "compose valide"
docker compose config | grep -E 'media|Audio__StoragePath|Cors__DebugOrigins|image: mentora-api'
```

Attendu : `compose valide`, puis les deux montages (`media-demo` et `media-prod` vers `/app/media`), deux `Audio__StoragePath: /app/media`, un `Cors__DebugOrigins: http://localhost:8100` et l'image `mentora-api:lot13` pour api-prod. Le filtre ne montre aucune ligne de secret ; ne jamais lancer `docker compose config` sans filtre.

### 6. nginx : taille maximale des requêtes **[SSH]**

L'API accepte une requête d'envoi de 8 Mo au plus (fichier de 5 Mo au plus) : `client_max_body_size 8m;` dans le bloc serveur HTTPS des deux confs de l'API, `mentora.conf` (api-demo) et `mentora-prod.conf` (api). Au delà de 8 Mo, nginx répond lui-même 413 (page HTML sans en-tête CORS : l'app mobile native voit le 413, un navigateur de débogage voit une erreur CORS) ; entre 5 et 8 Mo, l'API répond 422.

Sauvegarde hors de `conf.d`, remplacement, contrôle :

```bash
cd ~/mentora && cp nginx/conf.d/mentora.conf ~/mentora.conf.pre-lot13 && cp nginx/conf.d/mentora-prod.conf ~/mentora-prod.conf.pre-lot13
sed -i 's/client_max_body_size 25M;/client_max_body_size 8m;/' nginx/conf.d/mentora.conf nginx/conf.d/mentora-prod.conf
grep -n client_max_body_size nginx/conf.d/mentora.conf nginx/conf.d/mentora-prod.conf
```

Attendu : une ligne `client_max_body_size 8m;` par fichier. Si une autre valeur apparaît (le fichier du VPS diffère du dépôt), corriger dans nano avant de continuer.

```bash
docker exec mentora-nginx nginx -t && docker exec mentora-nginx nginx -s reload
```

Attendu : `syntax is ok`, `test is successful`. **Seulement si** `nginx -t` échoue (sinon ne pas lancer la commande suivante : elle remettrait 25M sur le disque), remettre les deux fichiers et ne pas recharger :

```bash
cp ~/mentora.conf.pre-lot13 nginx/conf.d/mentora.conf && cp ~/mentora-prod.conf.pre-lot13 nginx/conf.d/mentora-prod.conf && docker exec mentora-nginx nginx -t
```

Les fichiers d'exemple du dépôt (`nginx/conf.d/mentora.conf`, `nginx/conf.d/mentora-prod.conf.example`) portent déjà `8m`.

### 7. Bascule des images **[SSH]**

api-demo d'abord ; contrôles des étapes 8 et 9 pour api-demo ; api-prod seulement si tout est bon.

api-demo (l'ancienne image est gardée sous `pre-lot13`) :

```bash
cd ~/mentora && docker tag mentora-api:latest mentora-api:pre-lot13 && docker tag mentora-api:lot13 mentora-api:latest && docker compose up -d --no-deps --force-recreate api && sleep 25 && docker logs mentora-api 2>&1 | grep -c "Applying migration"
```

api-prod (l'image `mentora-api:lot13` est déjà dans le compose depuis l'étape 5) :

```bash
cd ~/mentora && docker compose up -d --no-deps --force-recreate api-prod && sleep 25 && docker logs mentora-api-prod 2>&1 | grep -c "Applying migration"
```

Attendu : `1` pour chaque service (une seule migration par base).

### 8. Lignes de démarrage attendues **[SSH]**

```bash
docker logs mentora-api 2>&1 | grep -E '\[AUDIO\]|\[CORS\]|\[MUX\]|\[EMAIL\] routing|\[ADMIN\]|Applying migration|Application started|fail:|crit:|Exception' | grep -v -E 'OTP|EMAIL-LOG'
```

Puis la même commande avec `mentora-api-prod`.

Attendu sur les deux :
- `[AUDIO] configured=True ffmpeg=True ffprobe=True storage=True maxConcurrent=2 timeoutSeconds=10` ;
- `Applying migration '20261117234001_Lot13_2_VoiceMessages'`, une fois par base ;
- `Application started`, aucune ligne `fail:` ni `crit:`.

En plus, sur PROD : `[CORS] origins=1 localhost=False`, `[CORS] origin https://admin.mentorapp.fr`, `[CORS] debugOrigins=1`, `[CORS] debug origin http://localhost:8100`. Sur api-demo : `[CORS] origins=0 localhost=True` et `[CORS] debugOrigins=0`.

Sur api-demo, `configured=False` arrête le déploiement (retour arrière, étape 11), même si l'API a démarré.

Après la migration, sur chaque base (même commande `docker exec -i mentora-postgres ...` qu'à l'étape 3) :

```sql
SELECT "MESSAGE_TYPE", count(*) FROM "MESSAGES" GROUP BY 1;
```

Attendu : `TEXT` seulement.

### 9. Contrôles sans jeton **[SSH]**

Depuis le VPS, par l'adresse publique (donc à travers nginx), d'abord api-demo :

```bash
B=https://api-demo.mentorapp.fr; z=00000000-0000-0000-0000-000000000000
curl -s -o /dev/null -w 'health %{http_code}\n' "$B/api/v1/health"
for p in member/coaches coach/members; do curl -s -o /dev/null -w "envoi $p %{http_code}\n" -X POST "$B/api/v1/$p/$z/conversation/messages/audio" -H 'Content-Type: multipart/form-data; boundary=b' --data-binary '--b--'; done
curl -s -o /dev/null -w 'ecoute %{http_code}\n' "$B/api/v1/conversations/$z/messages/$z/audio"
echo "preflight 8100 :"; curl -s -o /dev/null -D - -X OPTIONS "$B/api/v1/member/coaches/$z/conversation/messages/audio" -H 'Origin: http://localhost:8100' -H 'Access-Control-Request-Method: POST' -H 'Access-Control-Request-Headers: authorization' | grep -iE '^HTTP|^access-control-allow-origin'
echo "preflight 3000 :"; curl -s -o /dev/null -D - -X OPTIONS "$B/api/v1/health" -H 'Origin: http://localhost:3000' -H 'Access-Control-Request-Method: GET' | grep -iE '^HTTP|^access-control-allow-origin'
unset B z
```

Puis le même bloc avec `B=https://api.mentorapp.fr`.

Attendu, sur les deux : `health 200`, `envoi member/coaches 401`, `envoi coach/members 401`, `ecoute 401`, `preflight 8100` avec `HTTP/2 204` et `access-control-allow-origin: http://localhost:8100`, `preflight 3000` avec `HTTP/2 204` seul. Sur PROD, c'est `Cors__DebugOrigins` qui autorise l'origine 8100 ; sur api-demo, c'est le mode Development (section « Lot 13.1 »). Un `preflight 8100` sans en-tête sur PROD veut dire que `Cors__DebugOrigins` n'est pas pris en compte.

Les routes d'envoi sont testées avec un corps multipart : sans ce format, elles répondent 415 avant le contrôle du jeton (le format est vérifié au moment du choix de la route, avant l'autorisation). Ce 415 n'est pas une erreur et ne donne aucun accès.

### 10. Preuve fonctionnelle sur api-demo

Sur api-demo seulement, avec le compte membre des données de démo (pas une vraie boîte mail : le code de connexion est lu dans les logs, jamais affiché). Choisir un couple membre et coach dont la conversation existe déjà et contient des messages. En mode TEST, le code part aussi par email aux destinataires de test.

10.1 Envoyer le fichier d'essai **[PS]** (fichier AAC de 3 secondes produit et validé en 13.4 ; s'il manque, en produire un avec ffmpeg, AAC ADTS, entre 0,3 et 120 secondes, moins de 5 Mo) :

```powershell
Test-Path "$env:TEMP\mentora-lot13-audio\tone-3s.aac"
Push-Location "$env:TEMP\mentora-lot13-audio"; scp tone-3s.aac mentora@87.106.108.91:~/tone-3s.aac; Pop-Location
```

10.2 Tout le reste dans une seule session **[SSH]** (les variables restent en mémoire, rien de secret n'est affiché). Adresse du compte, tapée au clavier :

```bash
read -r -p "Adresse du membre de démo : " EM
```

Membre, coach lié et conversation (lecture seule) :

```bash
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -v em="$1"' sh "$EM" <<'SQL'
BEGIN READ ONLY;
SELECT mc."COACH_ID", mc."IS_PRIMARY", c."CONVERSATION_ID", c."CONVERSATION_LAST_MESSAGE_DATE",
       (SELECT count(*) FROM "MESSAGES" x WHERE x."CONVERSATION_ID" = c."CONVERSATION_ID") AS nb_messages
FROM "USERS" u
JOIN "MEMBERS" m ON m."USER_ID" = u."USER_ID"
JOIN "MEMBER_COACHES" mc ON mc."MEMBER_ID" = m."MEMBER_ID"
LEFT JOIN "CONVERSATIONS" c ON c."MEMBER_ID" = m."MEMBER_ID" AND c."COACH_ID" = mc."COACH_ID"
WHERE lower(u."USER_EMAIL") = lower(:'em');
ROLLBACK;
SQL
```

Prendre une ligne où `CONVERSATION_ID` n'est pas vide et `nb_messages` est supérieur à 0 :

```bash
read -r -p "COACH_ID : " COACH; read -r -p "CONVERSATION_ID : " CONV
```

Code de connexion lu dans les logs, jeton gardé en mémoire :

```bash
B=https://api-demo.mentorapp.fr; curl -s -o /dev/null -w '%{http_code}\n' -X POST "$B/api/v1/auth/otp/request" -H 'Content-Type: application/json' -d "{\"email\":\"$EM\"}"
CODE=$(docker logs --since 2m mentora-api 2>&1 | grep -E '\[OTP\] ' | grep -iF "$EM" | tail -1 | grep -oE '[0-9]{6}$'); echo "code lu=${#CODE} chiffres"
TOKEN=$(curl -s -X POST "$B/api/v1/auth/otp/verify" -H 'Content-Type: application/json' -d "{\"email\":\"$EM\",\"code\":\"$CODE\"}" | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4); unset CODE; echo "jeton lu=$([ -n "$TOKEN" ] && echo oui || echo non)"
```

Attendu : `200`, `code lu=6 chiffres`, `jeton lu=oui`.

Envoi du message vocal, puis écoute :

```bash
R=$(curl -s -w '\n%{http_code}' -X POST "$B/api/v1/member/coaches/$COACH/conversation/messages/audio" -H "Authorization: Bearer $TOKEN" -F "file=@$HOME/tone-3s.aac;type=audio/aac"); echo "$R" | tail -1; echo "$R" | head -1 | grep -oE '"(messageId|conversationId|messageType|mediaUrl|durationSeconds)":[^,]*'
MSG=$(echo "$R" | head -1 | grep -o '"messageId":"[^"]*"' | cut -d'"' -f4); MEDIA=$(echo "$R" | head -1 | grep -o '"mediaUrl":"[^"]*"' | cut -d'"' -f4)
curl -s -o ~/listen.aac -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" "$B$MEDIA"; sha256sum ~/tone-3s.aac ~/listen.aac
ls -l ~/mentora/media-demo/$CONV/
```

Attendu : `201`, `conversationId` égal à `$CONV`, `messageType` `AUDIO`, `durationSeconds` 3 ; puis `200` et deux empreintes SHA256 identiques ; le fichier `<messageId>.aac` est bien dans le dossier de l'hôte.

10.3 Nettoyage du message d'essai **[SSH]**. Un seul fichier SQL ; essai à blanc terminé par `ROLLBACK`, puis la même chose terminée par `COMMIT` (seule la dernière ligne diffère) :

```bash
cat > ~/cleanup-voice-test.sql <<'SQL'
\set ON_ERROR_STOP on
BEGIN;
SELECT "MESSAGE_ID", "MESSAGE_TYPE", "MESSAGE_AUDIO_PATH" FROM "MESSAGES" WHERE "MESSAGE_ID" = :'msg' AND "CONVERSATION_ID" = :'conv';
DELETE FROM "MESSAGES" WHERE "MESSAGE_ID" = :'msg' AND "CONVERSATION_ID" = :'conv' AND "MESSAGE_TYPE" = 'AUDIO';
UPDATE "CONVERSATIONS" SET "CONVERSATION_LAST_MESSAGE_DATE" = (SELECT max(x."MESSAGE_SENT_DATE") FROM "MESSAGES" x WHERE x."CONVERSATION_ID" = :'conv') WHERE "CONVERSATION_ID" = :'conv';
SELECT count(*) AS audio_restant FROM "MESSAGES" WHERE "MESSAGE_ID" = :'msg';
SQL
{ cat ~/cleanup-voice-test.sql; echo 'ROLLBACK;'; } | docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v msg="$1" -v conv="$2"' sh "$MSG" "$CONV"
```

Attendu : une ligne `AUDIO` avec le chemin `<CONVERSATION_ID>/<messageId>.aac`, `DELETE 1`, `UPDATE 1`, `audio_restant = 0`, `ROLLBACK`. Seulement si c'est le cas :

```bash
{ cat ~/cleanup-voice-test.sql; echo 'COMMIT;'; } | docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v msg="$1" -v conv="$2"' sh "$MSG" "$CONV"
```

Puis le fichier, supprimé par le conteneur (il appartient à root), et la preuve :

```bash
docker exec mentora-api sh -c 'rm -v "/app/media/$1/$2.aac"' sh "$CONV" "$MSG"
docker exec mentora-api sh -c 'test -e "/app/media/$1/$2.aac" && echo PRESENT || echo ABSENT' sh "$CONV" "$MSG"; ls -l ~/mentora/media-demo/$CONV/
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" "$B$MEDIA"
docker exec mentora-api sh -c 'rmdir -v "/app/media/$1"' sh "$CONV"; ls -la ~/mentora/media-demo/
rm ~/tone-3s.aac ~/listen.aac ~/cleanup-voice-test.sql; unset TOKEN EM R MSG MEDIA COACH CONV B
```

Attendu : `removed ...`, `ABSENT`, le fichier n'apparaît plus, l'écoute répond `404`, `rmdir: removing directory ...` (dossier de la conversation, vide, qui appartient à root), puis `media-demo` ne contient plus que `.incoming`. Les lignes de connexion créées (code et session) restent dans la base, comme lors des essais locaux.

Sur PROD : nous n'envoyons aucun message vocal. Seuls les contrôles de l'étape 9. Les essais fonctionnels sur PROD sont faits par PJ avec ses propres comptes.

### 11. Retour arrière

Par service, l'image précédente :

```bash
cd ~/mentora && docker tag mentora-api:pre-lot13 mentora-api:latest && docker compose up -d --no-deps --force-recreate api
```

Pour api-prod : `nano docker-compose.yml`, remettre la ligne `image:` d'api-prod à l'étiquette notée à l'étape 5, puis :

```bash
cd ~/mentora && docker compose config --quiet && docker compose up -d --no-deps --force-recreate api-prod
```

Les lignes `Audio__StoragePath`, `Cors__DebugOrigins` et les volumes peuvent rester : l'ancien code ne les lit pas. Retour arrière de nginx (facultatif, 8m convient aussi à l'ancien code) : copier les fichiers `~/mentora*.conf.pre-lot13`, `nginx -t`, reload.

L'ancienne image fonctionne sur la base migrée (vérifié dans le code de `origin/main`, `git show origin/main:<fichier>`) :
- `Mentora.Core/Entities/Message.cs` et `MessageConfiguration.cs` de main ne connaissent aucune des six nouvelles colonnes : EF ne les lit pas et ne les écrit pas.
- Un message texte inséré par l'ancien code n'envoie pas `MESSAGE_TYPE` : la base met la valeur par défaut `'TEXT'`, et les cinq colonnes audio restent NULL. Les deux contraintes passent (`CK_MESSAGES_TYPE` : `TEXT` ; `CK_MESSAGES_AUDIO_FIELDS` : branche TEXT, cinq colonnes NULL, contenu non testé).
- Au démarrage, `MigrateAsync` (EF Core 8.0.0 sur main) n'applique que les migrations de l'assemblage absentes de l'historique : la ligne `20261117234001_Lot13_2_VoiceMessages` de l'historique, inconnue de l'ancien code, est ignorée.
- Effet visible : un message vocal déjà envoyé apparaît comme un message texte vide dans l'ancienne version.

La migration `Down` n'est donc jamais nécessaire pour revenir à l'image précédente (elle est refusée dès qu'un message vocal existe, c'est voulu). Pour revenir à l'état exact d'avant, restaurer les sauvegardes de l'étape 2 (avec François, après avoir arrêté le service).

### 12. Après le déploiement

Supprimer les archives **[SSH]** :

```bash
rm ~/mentora-api-lot13.tar.gz; ls ~/mentora-api-lot13.tar.gz
```

Attendu : `No such file or directory`.

**[PS]** :

```powershell
Remove-Item "$env:TEMP\mentora-api-lot13.tar.gz"; Test-Path "$env:TEMP\mentora-api-lot13.tar.gz"; Test-Path "$env:TEMP\mentora-api-lot13.tar"
```

Attendu : `False` et `False`.

Garder `mentora-api:pre-lot13` (api-demo) et l'ancienne étiquette d'api-prod notée à l'étape 5 jusqu'au merge du lot 13 dans main.

---

## Lot 14 vague 1 (21/11)

Contenu : 14.1 (invitation d'un membre par le coach, e-mail unique sans casse), 14.1b (champs facultatifs à l'ajout d'un membre), 14.2 (protection des codes de connexion), 14.3 (la ligne console `[OTP]` n'existe plus qu'en Development).

### a. Avant de déployer **[SSH]**

1. Sauvegardes des deux bases, comme à l'étape 2 du lot 13 : `mentora_prod` et `mentora_db` (api-demo).
2. Contrôles en lecture seule, sur les deux bases (même commande `docker exec -i mentora-postgres ...` qu'à l'étape 3 du lot 13) :
   - `docs/review/lot14/14.1/precheck.sql` : la requête 1 doit être vide, sinon **arrêter** le déploiement (des e-mails ne différant que par la casse empêcheraient l'index unique) ;
   - `docs/review/lot14/14.2/precheck.sql`.

### b. Variables facultatives (aucune n'est obligatoire)

- `MemberInvite__InstallUrl` : URL absolue en `https`. Ne pas la définir tant que PJ n'a pas donné le lien. Sans elle, le mail d'invitation dit que le coach enverra le lien.
- `MemberInvite__PermitLimit` et `MemberInvite__WindowMinutes` : limite d'invitations, défaut 30 par 60 minutes.
- `AuthRateLimit__PermitLimit` et `AuthRateLimit__WindowMinutes` : limite des demandes de code, défaut 30 par 15 minutes.

Les valeurs par défaut conviennent : aucune variable à ajouter.

### c. Lignes de démarrage attendues **[SSH]**

Sur api-prod et sur api-demo, une fois par base : `Applying migration` pour `Lot14_1_UserEmailCi` et `Lot14_2_OtpAttempts`. Puis :

```
[INVITE] installUrl=False permitLimit=30 windowMinutes=60
[AUTH] otpRateLimit permitLimit=30 windowMinutes=15
```

en plus des lignes habituelles (`[EMAIL] routing`, `[CORS]`, `[AUDIO]`, `[MUX]`, `Application started`), sans ligne `fail:` ni `crit:`.

### d. Preuve que les logs de PROD ne contiennent aucun code **[SSH]**

Faire une demande de code pour un compte de l'équipe, puis compter (jamais afficher) :

```bash
cd ~/mentora && docker compose logs --since 5m api-prod | grep -c "\[OTP\]"
cd ~/mentora && docker compose logs --since 5m api | grep -c "\[OTP\]"
```

Attendu : `0` pour api-prod ; `1` ou plus pour api (demo, Development). Ne jamais afficher les lignes `[OTP]`.

### e. Retour arrière

Image précédente, comme à l'étape 11 du lot 13. Aucune migration `Down` : les deux migrations sont additives (un index unique sur `lower(email)`, et une colonne avec la valeur par défaut 0). L'image précédente continue de fonctionner avec elles. Les variables ci-dessus peuvent rester : l'ancien code ne les lit pas.

---

## Bascule des mails en Production (24/11)

À faire seulement quand la vague 1 est déployée et contrôlée. api-demo reste en mode TEST (tous les mails vers les seules adresses de test). Seul api-prod passe en Production : le vrai destinataire reçoit le mail, et les deux adresses en copie cachée (Bcc) en reçoivent une copie.

### 1. Secret dans `~/mentora/.env` **[SSH]**

Variable : `EMAIL_ROUTING_PRODUCTION_BCC` (adresses séparées par une virgule). Saisie masquée, jamais affichée, contrôlée par sa longueur.

Avant d'ajouter la ligne, vérifier que `.env` se termine par un retour à la ligne :

```bash
cd ~/mentora && tail -c1 .env | wc -l
```

Attendu : `1`. Si `0`, ajouter d'abord le retour à la ligne avec `echo >> .env`.

```bash
cd ~/mentora && read -rs BCC && printf 'EMAIL_ROUTING_PRODUCTION_BCC=%s\n' "$BCC" >> .env && unset BCC
grep '^EMAIL_ROUTING_PRODUCTION_BCC=' .env | cut -d= -f2- | tr -d '\n' | wc -c
```

Coller la valeur une seule fois (Ctrl+V OU clic droit dans le terminal de VS Code, jamais les deux).

Attendu : une longueur cohérente avec les deux adresses. Ne jamais faire `cat .env`.

### 2. Compose, api-prod uniquement **[SSH]**

Sauvegarde du compose avant modification :

```bash
cd ~/mentora && cp docker-compose.yml docker-compose.yml.pre-mail-prod
```

Avec `nano docker-compose.yml`, dans `environment` d'`api-prod` seulement (six espaces d'indentation) :

```yaml
      - EmailRouting__Mode=Production
      - EmailRouting__ProductionBcc=${EMAIL_ROUTING_PRODUCTION_BCC}
```

Ne rien ajouter à api-demo. Puis :

```bash
cd ~/mentora && docker compose config --quiet && docker compose up -d --no-deps --force-recreate api-prod
```

`config --quiet` ne doit rien afficher (ne jamais lancer `docker compose config` sans `--quiet` : il écrit les secrets).

### 3. Ligne de démarrage attendue **[SSH]**

```bash
cd ~/mentora && docker compose logs --since 5m api-prod | grep -E "\[EMAIL\]|warn:|fail:"
```

Attendu : `[EMAIL] routing mode=PRODUCTION provider=Smtp testRecipients=4 productionBcc=2`. Ne jamais filtrer sur `[EMAIL-LOG]` ni sur `[OTP]`.

Un refus au démarrage (`EmailRouting:ProductionBcc ...`) nomme le réglage et le numéro de l'élément, jamais l'adresse : corriger la variable, recréer api-prod.

### 4. Preuve avec un vrai mail

Demander un code de connexion pour un compte de l'équipe, depuis l'app. Vérifier :

- le mail arrive chez le vrai destinataire, sans préfixe `[TEST pour ...]` dans le sujet ;
- une copie arrive dans chacune des deux boîtes en copie cachée ;
- l'en-tête `Bcc` n'apparaît **pas** dans le source du message reçu par le destinataire.

### 5. Retour arrière

Retirer les deux lignes `EmailRouting__Mode` et `EmailRouting__ProductionBcc` du compose d'api-prod (ou remettre `docker-compose.yml.pre-mail-prod`), puis :

```bash
cd ~/mentora && docker compose config --quiet && docker compose up -d --no-deps --force-recreate api-prod
```

Attendu : `[EMAIL] routing mode=TEST ... productionBcc=0`. La variable du `.env` peut rester.
