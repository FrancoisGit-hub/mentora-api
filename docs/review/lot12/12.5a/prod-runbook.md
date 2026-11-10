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
