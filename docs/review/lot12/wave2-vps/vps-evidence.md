# Lot 12, vague 2 (vidéos MUX) : preuves du déploiement PROD et api-demo, 12/11/2026

Code déployé : branche feature/lot_12 à cea79d1 (12.3.0 e935a4f, 12.3a 6e04b63 et f35dd41, 12.3b 3228082, correctifs de revue b9b510f, preuves cea79d1). Image `mentora-api:lot12-v2` (identifiant 502f131bb3ae), construite le 12/11 à 07:13 UTC. Aucune valeur de secret dans ce fichier : les identifiants MUX d'upload, d'asset et de vidéo ne sont pas des secrets ; l'URL de dépôt signée n'est jamais écrite.

Heures en UTC (heure du VPS).

## 1. Avant le déploiement

- Commits des correctifs de revue relus (diff complet des 4 fichiers de code), contrôles de fuite sur les 6 fichiers commités (JWT, marqueurs OTP et EMAIL-LOG, adresses gmail, web2success.fr et mentorapp.fr, secrets MUX avec valeur, URL signées) : 0 correspondance. Builds API et Admin : 0 erreur, 0 avertissement.
- Sauvegardes : `~/backup-demo-20261112-0708.dump` (527K, mentora_db) et `~/backup-prod-20261112-0708.dump` (396K, mentora_prod).
- Contrôle en lecture seule des deux bases : dernière migration `20261109160428_Lot12_2b_AdminAuditLog`, `EXERCISE_VIDEOS` vide, `ADMIN_AUDIT_LOG` vide. Les nouveaux CHECK de la migration 12.3a ne peuvent donc pas échouer.
- Image : contrôle que le correctif V2 est dans l'image (`tr -d \\000 < /app/Mentora.Infrastructure.dll | grep -c deletable` = 1). Construction entièrement en cache (même code qu'une construction précédente), d'où ce contrôle.

## 2. Secrets et compose

- `.env` sauvegardé (`.env.bak-20261112-080144`), puis 6 variables ajoutées par saisie masquée : MUX_TOKEN_ID_PROD, MUX_TOKEN_SECRET_PROD, MUX_WEBHOOK_SECRET_PROD, MUX_TOKEN_ID_DEMO, MUX_TOKEN_SECRET_DEMO, MUX_WEBHOOK_SECRET_DEMO. Contrôle par nom et longueur seulement : 36, 75, 32 pour chaque environnement, sans espace ni `$`.
- Incident corrigé : la première saisie avait collé chaque valeur deux fois (Ctrl+V puis clic droit dans le terminal de VS Code, longueurs 72, 150, 64). Détecté par le contrôle de longueur, prouvé par la comparaison des deux moitiés (identiques sur les 6), corrigé en gardant la première moitié (sauvegarde `.env.bak-avant-fix-*`).
- Compose sauvegardé (`docker-compose.yml.pre-lot12-v2`), puis modifié par une commande qui vérifie les comptes : 8 lignes ajoutées (Mux__TokenId, Mux__TokenSecret, Mux__WebhookSecret en renvois `${...}`, Mux__CorsOrigin), image d'api-prod passée de `mentora-api:lot12` à `mentora-api:lot12-v2`. `docker compose config --quiet` : valide.
- Mux__CorsOrigin : `https://admin.mentorapp.fr` pour api-prod, `http://localhost:4200` pour api-demo (décision 1A du 12/11 : pas d'admin-demo).

## 3. api-demo (répétition)

- Ancienne image gardée sous `mentora-api:pre-lot12-v2` (= `mentora-api:lot12`, daf1f0666fe6), `up -d --no-deps --force-recreate api`.
- Logs : `Applying migration '20261110104601_Lot12_3a_MuxVideos'` (1), `[MUX] configured=True corsOrigin=http://localhost:4200 apiBase=api.mux.com`, `[CORS] origins=0 localhost=True`, `[EMAIL] routing mode=TEST`, `[ADMIN] accounts listed=4 created=0`, patchs `skipped`, seeders sans création, `Application started`, aucune ligne fail, crit ni Exception. Second redémarrage : 0 migration, même résultat.
- Depuis le poste : health 200, POST video-uploads sans jeton 401, POST webhook non signé 401.

## 4. api-prod

- `up -d --no-deps --force-recreate api-prod`.
- Logs : 1 migration (Lot12_3a_MuxVideos), `[MUX] configured=True corsOrigin=https://admin.mentorapp.fr apiBase=api.mux.com`, `[CORS] origins=1 localhost=False`, `[CORS] origin https://admin.mentorapp.fr`, `[EMAIL] routing mode=TEST provider=Smtp testRecipients=4 productionBcc=0`, `[ADMIN] accounts listed=4 created=0`, `Application started`, aucune ligne fail ni crit.
- Depuis le poste : health 200, POST video-uploads sans jeton 401, POST webhook non signé 401 (ligne `[MUX] webhook rejected reason=missing` dans les logs).

## 5. Admin et nginx

- Publication Release (dossier vidé avant) : deux fichiers de configuration seulement (`appsettings.json`, `appsettings.Production.json` avec `ApiBaseUrl` https://api.mentorapp.fr), `HTTP_308_STALLED` présent 2 fois dans `wwwroot/js/admin.js` (correctif V1).
- Installation dans `~/mentora/admin-www`, version précédente dans `admin-www.prev` ; conf `mentora-admin.conf` remplacée (ancienne gardée dans `~/mentora-admin.conf.pre-v2`), `nginx -t` puis reload.
- Incident corrigé : nginx servait encore l'ancienne admin (admin.js de 3 381 octets du 09/11). Cause : le volume `./admin-www:/var/www/mentora-admin:ro` suit le dossier par son inode ; après `mv admin-www admin-www.prev`, nginx servait `admin-www.prev`. Correction : `docker compose restart nginx` (coupure de quelques secondes), puis `HTTP_308_STALLED` trouvé 2 fois dans le fichier vu par nginx et admin.js servi en ligne de 10 734 octets avec le correctif. Mode d'emploi corrigé (étape E de la section Admin).
- En-têtes servis : `Cache-Control: no-cache` et CSP avec `img-src 'self' data: https://image.mux.com` et `connect-src 'self' https://api.mentorapp.fr https://*.mux.com`. api et api-demo : health 200 après le redémarrage de nginx.

## 6. Test dans Chrome en PROD (Claude in Chrome, 12/11)

Décision 2A du 12/11 : une vidéo de test déposée puis retirée.

- Page de connexion puis connexion par code (code tapé par François), aucune violation de CSP (écouteur securitypolicyviolation et ReportingObserver) pendant tout le test.
- Exercice EX216 « Ab wheel rollout » (8bb68199-1e05-48f4-935b-5d701688dd7e), sans vidéo au départ. Fichier choisi par François.
- Dépôt direct navigateur vers MUX : `[MUX] upload created video=b5c7b60e-f751-4b06-b090-29d04776ca49` à 09:07:10. Carte « Vidéo prête, Durée : 54 s », miniature image.mux.com chargée (480 px), lien vers le lecteur MUX en nouvel onglet.
- Vrais webhooks MUX reçus et signés (première preuve en conditions réelles), table MUX_WEBHOOK_EVENTS :

| Type | Statut | Résultat | Reçu |
|---|---|---|---|
| video.upload.created | IGNORED | type not handled | 09:07:13 |
| video.upload.asset_created | PROCESSED | video b5c7b60e... WAITING | 09:07:23 |
| video.asset.created | IGNORED | type not handled | 09:07:23 |
| video.asset.ready | PROCESSED | video b5c7b60e... READY | 09:07:26 |

- EXERCISE_VIDEOS : READY, playback présent, durée 54.42, format 1:1, sans erreur.
- Retrait depuis la fiche (confirmation) : « Vidéo retirée », liste EX216 en « Sans vidéo » après rechargement complet. Logs : `[MUX] video removed`, `[MUX] remove video=b5c7b60e... asset ... deleted`, puis `video.asset.deleted result=no matching video` (attendu).
- Après le retrait : EXERCISE_VIDEOS 0 ligne ; ADMIN_AUDIT_LOG : EXERCISE_VIDEO_UPLOAD_CREATED (09:07:10) et EXERCISE_VIDEO_REMOVED (09:11:04), gardées ; API MUX de l'environnement PROD : liste des assets vide (`"data":[]`).
- Déconnexion (refresh retiré du navigateur), onglet fermé.

## 7. Non prouvé ou hors de ce test

- Réponse des routes des apps en PROD : la base PROD n'a encore aucun compte coach ni élève. Le contrat (video seulement en READY, sinon null ; clé video en dernier dans l'arbre du programme) est prouvé en local (12.3a, P4 et P5).
- Concurrence du webhook (deux événements identiques, course webhook et sync) : non provoquée, au backlog.
- Webhook DEMO : variables en place sur api-demo, aucun dépôt fait sur l'environnement MUX DEMO pendant ce déploiement.
