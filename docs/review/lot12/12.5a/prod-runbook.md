# Runbook Beta PROD (lot 12, 12.5a)

But : mettre en ligne l'API de la Beta PROD sur `https://api.mentorapp.fr`, sur le même VPS que api-demo, avec une base neuve. Aucune valeur de secret n'est écrite ici : chaque secret est généré sur le serveur et écrit directement dans `~/mentora/.env`, sans jamais être affiché.

Légende : **[PS]** = terminal PowerShell de VS Code sur ton poste. **[SSH]** = session SSH sur le VPS (`ssh <utilisateur>@87.106.108.91`). Les noms de conteneur et de service ci-dessous sont ceux du plan (`mentora-postgres`, `nginx`, `api`, `api-prod`) : vérifie-les une fois avec `docker ps` et `docker compose ps` avant de commencer.

Règles pendant tout le runbook : ne jamais faire `cat ~/mentora/.env`, ne jamais coller un secret dans un chat, un ticket ou un message.

---

## 1. Vérifier le DNS **[PS]**

```powershell
Resolve-DnsName api.mentorapp.fr -Type A
Resolve-DnsName admin.mentorapp.fr -Type A
```

Attendu : les deux noms répondent `87.106.108.91`. Sinon, ne pas continuer (le certificat de l'étape 6 échouerait).

## 2. Sauvegarder la base api-demo **[SSH]**

Toujours avant de toucher à quoi que ce soit.

```bash
cd ~/mentora
mkdir -p ~/mentora/backups
docker exec mentora-postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > ~/mentora/backups/api-demo-$(date +%Y%m%d-%H%M%S).dump
ls -l ~/mentora/backups | tail -3
```

Attendu : un fichier `.dump` de taille non nulle. Si `POSTGRES_USER` ou `POSTGRES_DB` ne sont pas définis dans le conteneur, regarder les variables du service postgres dans `docker-compose.yml` (noms seulement) et les écrire en clair dans la commande.

## 3. Créer le rôle et la base PROD **[SSH]**

Le mot de passe est généré par `openssl`, écrit dans `.env`, relu dans une variable, et passé à PostgreSQL par l'entrée standard. Il n'est jamais affiché.

```bash
cd ~/mentora
cp .env .env.bak-$(date +%Y%m%d-%H%M%S)
grep -q '^POSTGRES_PROD_PASSWORD=' .env && echo "DEJA PRESENT: ne pas regenerer" || echo "POSTGRES_PROD_PASSWORD=$(openssl rand -hex 24)" >> .env
PW=$(grep '^POSTGRES_PROD_PASSWORD=' .env | cut -d= -f2-)
docker exec -i mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1' <<SQL
CREATE ROLE mentora_prod_user LOGIN PASSWORD '$PW';
CREATE DATABASE mentora_prod OWNER mentora_prod_user;
SQL
unset PW
docker exec mentora-postgres sh -c 'psql -U "$POSTGRES_USER" -d postgres -Atc "select rolname from pg_roles where rolname = '"'"'mentora_prod_user'"'"'; select datname from pg_database where datname = '"'"'mentora_prod'"'"';"'
```

Attendu : les deux dernières lignes affichent `mentora_prod_user` puis `mentora_prod`. Si le rôle ou la base existe déjà, PostgreSQL le dit : s'arrêter et comprendre avant de relancer (ne jamais supprimer sans sauvegarde).

## 4. Variables à ajouter dans `~/mentora/.env` pour PROD **[SSH]**

Noms seulement. Chaque ligne s'ajoute avec `echo "NOM=..." >> ~/mentora/.env` ou avec un éditeur (`nano ~/mentora/.env`). Sauvegarde déjà faite à l'étape 3.

| Nom dans `.env` | D'où vient la valeur |
|---|---|
| `POSTGRES_PROD_PASSWORD` | déjà créé à l'étape 3 |
| `JWT_SECRET_PROD` | nouveau, aléatoire : `echo "JWT_SECRET_PROD=$(openssl rand -base64 48)" >> ~/mentora/.env` |
| `ADMIN_JWT_SECRET_PROD` | nouveau, aléatoire, **différent** du précédent : `echo "ADMIN_JWT_SECRET_PROD=$(openssl rand -base64 48)" >> ~/mentora/.env` |
| `ADMIN_EMAILS_PROD` | les 4 boîtes mentorapp.fr des administrateurs, séparées par des virgules, sans espace |
| `EMAIL_ROUTING_TEST_RECIPIENTS_PROD` | la même valeur que celle utilisée par api-demo (le PROD reste en mode TEST jusqu'à l'Alpha) |
| SMTP (`Email__Smtp__Host`, `Port`, `UseStartTls`, `Username`, `Password`) et `Email__FromAddress` | les mêmes valeurs que api-demo : réutiliser les lignes déjà présentes dans `.env`, ne rien recopier |
| LiveKit (`LiveKit__Url`, `LiveKit__ApiKey`, `LiveKit__ApiSecret`) | les mêmes valeurs que api-demo : réutiliser les lignes déjà présentes |

Vérifier sans rien afficher que les noms sont présents :

```bash
for n in POSTGRES_PROD_PASSWORD JWT_SECRET_PROD ADMIN_JWT_SECRET_PROD ADMIN_EMAILS_PROD EMAIL_ROUTING_TEST_RECIPIENTS_PROD; do grep -q "^$n=" ~/mentora/.env && echo "$n present" || echo "$n MANQUANT"; done
```

Le service de l'étape 5 construit lui-même la chaîne de connexion à `mentora_prod` à partir de `POSTGRES_PROD_PASSWORD`, et fixe `Cors__AllowedOrigins=https://admin.mentorapp.fr` (une origine publique, pas un secret).

L'API refuse de démarrer en Production si `Jwt__Secret` manque ou fait moins de 32 caractères, si `ConnectionStrings__DefaultConnection` manque, si `Cors__AllowedOrigins` est vide, en `http://` ou avec un joker, ou si `AdminAuth__JwtSecret` est identique à `Jwt__Secret`. Dans ces cas elle s'arrête avant les migrations.

## 5. Ajouter le service `api-prod` à `docker-compose.yml` **[SSH]**

Sauvegarde d'abord :

```bash
cd ~/mentora
cp docker-compose.yml docker-compose.yml.bak-$(date +%Y%m%d-%H%M%S)
```

Ajouter sous `services:` (éditeur `nano docker-compose.yml`), en copiant le bloc du service `api` existant pour l'image, le réseau et les volumes éventuels, puis en changeant ce qui suit. L'image est la même que celle de api-demo. Le nom d'hôte PostgreSQL (`postgres` ci-dessous) doit être celui que le service `api` utilise déjà.

```yaml
  api-prod:
    image: <la meme image que le service api>
    container_name: mentora-api-prod
    restart: unless-stopped
    depends_on:
      - postgres
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=mentora_prod;Username=mentora_prod_user;Password=${POSTGRES_PROD_PASSWORD}
      - Jwt__Secret=${JWT_SECRET_PROD}
      - AdminAuth__JwtSecret=${ADMIN_JWT_SECRET_PROD}
      - AdminAuth__Emails=${ADMIN_EMAILS_PROD}
      - Cors__AllowedOrigins=https://admin.mentorapp.fr
      - EmailRouting__Mode=Test
      - EmailRouting__TestRecipients=${EMAIL_ROUTING_TEST_RECIPIENTS_PROD}
      - Email__Provider=${Email__Provider}
      - Email__FromAddress=${Email__FromAddress}
      - Email__Smtp__Host=${Email__Smtp__Host}
      - Email__Smtp__Port=${Email__Smtp__Port}
      - Email__Smtp__UseStartTls=${Email__Smtp__UseStartTls}
      - Email__Smtp__Username=${Email__Smtp__Username}
      - Email__Smtp__Password=${Email__Smtp__Password}
      - LiveKit__Url=${LiveKit__Url}
      - LiveKit__ApiKey=${LiveKit__ApiKey}
      - LiveKit__ApiSecret=${LiveKit__ApiSecret}
    # pas de "ports:" : seul nginx parle a ce conteneur
    networks:
      - <le meme reseau que nginx>
```

Contrôles sans afficher de secret :

```bash
docker compose config --quiet && echo "compose valide"
docker compose config | grep -c 'api-prod'
```

Attendu : `compose valide`, et un nombre supérieur à 0. Ne jamais lancer `docker compose config` sans filtre dans un terminal partagé : il affiche les valeurs.

## 6. Certificat et nginx **[SSH]**

Ordre important : nginx ne doit jamais charger le bloc HTTPS avant que le certificat existe.

6.1 Bloc HTTP seul pour les deux noms, dans un fichier temporaire de `nginx/conf.d/` (par exemple `mentora-prod-http.conf`) :

```nginx
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
```

6.2 Recharger nginx :

```bash
cd ~/mentora
docker compose exec nginx nginx -t && docker compose exec nginx nginx -s reload
```

6.3 Demander le certificat (webroot, un certificat pour les deux noms). Reprendre la commande certbot déjà utilisée pour api-demo (même dossier webroot `/var/www/certbot`, mêmes volumes), en changeant seulement les noms :

```bash
docker compose run --rm certbot certonly --webroot -w /var/www/certbot -d api.mentorapp.fr -d admin.mentorapp.fr --cert-name api.mentorapp.fr
```

Attendu : `Successfully received certificate`. Le certificat est enregistré sous `/etc/letsencrypt/live/api.mentorapp.fr/`, le chemin utilisé par le fichier d'exemple.

6.4 Remplacer le bloc temporaire par la configuration définitive : supprimer `mentora-prod-http.conf`, puis copier le fichier d'exemple du dépôt :

```bash
cd ~/mentora
rm nginx/conf.d/mentora-prod-http.conf
cp nginx/conf.d/mentora-prod.conf.example nginx/conf.d/mentora-prod.conf
docker compose exec nginx nginx -t && docker compose exec nginx nginx -s reload
```

Attendu : `syntax is ok` et `test is successful`. Si `nginx -t` échoue, retirer `mentora-prod.conf` et recharger avant toute autre action. Le nom `admin.mentorapp.fr` sera servi à l'étape 12.5b : en attendant, il ne répond rien d'utile, c'est normal.

## 7. Démarrer api-prod et contrôler **[SSH]**

```bash
cd ~/mentora
docker compose up -d api-prod
sleep 15
docker logs mentora-api-prod 2>&1 | grep -E 'Applying migration|No migrations were applied|Seeder|\[EMAIL\] routing|\[ADMIN\]|\[CORS\]|\[PATCH\]|fail:|crit:'
```

Attendu : des lignes `Applying migration` (base neuve), `[EMAIL] routing mode=TEST`, `[CORS] origins=1 localhost=False`, `[CORS] origin https://admin.mentorapp.fr`, `[ADMIN] auth emails=4`, aucune ligne `fail:` ni `crit:`. Ne jamais afficher les lignes contenant le marqueur OTP ou le marqueur d'envoi d'email.

Contrôles HTTP, depuis ton poste **[PS]** :

```powershell
(Invoke-WebRequest https://api.mentorapp.fr/api/v1/health -UseBasicParsing).StatusCode
```

Attendu : `200`.

```powershell
try { Invoke-WebRequest https://api.mentorapp.fr/api/v1/admin/exercises -UseBasicParsing } catch { [int]$_.Exception.Response.StatusCode }
```

Attendu : `401` (route admin sans jeton).

```powershell
$r = Invoke-WebRequest https://api.mentorapp.fr/api/v1/admin/auth/otp/request -Method Options -UseBasicParsing -Headers @{ Origin = "https://admin.mentorapp.fr"; "Access-Control-Request-Method" = "POST"; "Access-Control-Request-Headers" = "content-type" }
"$($r.StatusCode) $($r.Headers['Access-Control-Allow-Origin'])"
```

Attendu : `204 https://admin.mentorapp.fr`.

## 8. Retour arrière **[SSH]**

À faire dans cet ordre, en s'arrêtant dès que le problème est réglé :

```bash
cd ~/mentora
docker compose stop api-prod
docker compose rm -f api-prod
rm -f nginx/conf.d/mentora-prod.conf nginx/conf.d/mentora-prod-http.conf
docker compose exec nginx nginx -t && docker compose exec nginx nginx -s reload
cp docker-compose.yml.bak-<horodatage> docker-compose.yml
```

Remplacer `<horodatage>` par celui de la sauvegarde de l'étape 5 (`ls ~/mentora/docker-compose.yml.bak-*`). api-demo n'est jamais touché par ce retour arrière. La base `mentora_prod` peut rester : elle est vide ou jetable, et ne sert qu'à api-prod.

---

## Pour api-demo : avant de déployer le code du lot 12

Deux contrôles, avant de mettre le nouveau code sur api-demo.

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

**B. Variables d'administration pour api-demo.** Ajouter dans `.env` puis dans l'environnement du service `api` : `AdminAuth__JwtSecret` (aléatoire, `openssl rand -base64 48`, différent de `Jwt__Secret`) et `AdminAuth__Emails` (les 4 boîtes mentorapp.fr). Sans elles, l'API refuse de démarrer. Faire la sauvegarde `pg_dump` de l'étape 2 juste avant.

---

## Admin (12.5b)

But : servir l'admin (Blazor WebAssembly, fichiers statiques) sur `https://admin.mentorapp.fr`, avec le conteneur nginx qui tourne déjà. À faire après le runbook ci-dessus (le certificat de l'étape 6 couvre déjà `admin.mentorapp.fr`, et api-prod répond).

Légende : **[PS]** = terminal PowerShell de VS Code sur ton poste, à la racine du dépôt. **[SSH]** = session SSH sur le VPS.

### A. Publier l'admin **[PS]**

```powershell
dotnet publish Mentora.Admin -c Release -o docs/review/lot12/12.5b/publish
```

Le dossier `publish/` est ignoré par git. Le résultat utile est le sous-dossier `wwwroot`.

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
cp docker-compose.yml docker-compose.yml.bak-$(date +%Y%m%d-%H%M%S)
```

Dans `docker-compose.yml` (`nano docker-compose.yml`), sous `volumes:` du service `nginx`, ajouter cette ligne (en lecture seule) à côté des autres volumes :

```yaml
      - ./admin-www:/var/www/mentora-admin:ro
```

Puis :

```bash
docker compose config --quiet && echo "compose valide"
docker compose up -d nginx
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

Ouvrir `https://admin.mentorapp.fr` (fenêtre privée de préférence). La page de connexion s'affiche avec le logo et le thème, la console ne contient aucune erreur de Content-Security-Policy. Demander un code de connexion et se connecter avec une des boîtes d'administration.

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
cp docker-compose.yml.bak-<horodatage> docker-compose.yml
docker compose up -d nginx
```

Remplacer `<horodatage>` par celui de la sauvegarde de l'étape F (`ls ~/mentora/docker-compose.yml.bak-*`). Ce retour arrière ne touche ni api-prod ni api-demo.
