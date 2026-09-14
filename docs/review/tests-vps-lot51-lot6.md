# Tests VPS groupés — lot 5.1 + lot 6

Branche testée : `feature/lot_6` (6be3cf6), déjà déployée sur le VPS.
Base : `mentora_db` sur `api-demo.mentorapp.fr`.
Aucune migration à appliquer, aucun redéploiement avant la fin de la passe.

**Base URL à utiliser : `https://api-demo.mentorapp.fr`.**
Pas `http://87.106.108.91`. L'environnement Postman `Mentora-VPS_dev` pointe sur
l'IP en HTTP : il court-circuite nginx, donc `UseForwardedHeaders`, et les URL
absolues renvoyées (visio, liens) ne seront pas représentatives. Crée un
environnement `Mentora-VPS_https` avant de commencer.

Règle de conduite : une vérification à la fois, on note le résultat réel, on ne
corrige rien pendant la passe. Les corrections se font en une seule fois après.

---

## Phase 0 — Préconditions et baseline

### P0.1 Santé et version déployée
- `GET /api/v1/health` → 200
- `SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 8;`
  Les cinq migrations du lot 6 doivent être présentes, dont
  `20260812095011_Lot6_4_GroupSessions`.

### P0.2 Clôture de F3 (CHECK sur SESSIONS)
```sql
SELECT conname, pg_get_constraintdef(oid)
FROM pg_constraint
WHERE contype = 'c' AND conrelid = '"SESSIONS"'::regclass;
```
Attendu : `CK_SESSIONS_GROUP_HAS_NO_MEMBER` présent. Sa présence prouve que
Postgres l'a validé sur les données réelles au moment de la migration. F3 est
alors close, sans requête d'audit supplémentaire.

### P0.3 Définition de la vue
```sql
SELECT pg_get_viewdef('"V_SESSION_MEMBERS"'::regclass, true);
```
Lire la définition et confirmer que chaque branche de l'UNION porte bien
`SESSION_COACH_ID` (ou l'équivalent) en colonne exposée. Une branche qui perd
le coach est un défaut de scope, à noter immédiatement.

### P0.4 Baseline chiffrée
```sql
SELECT 'EXERCISES' AS t, count(*) FROM "EXERCISES"
UNION ALL SELECT 'EXERCISES_MENTORA', count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL
UNION ALL SELECT 'PROGRAM_TEMPLATES', count(*) FROM "PROGRAM_TEMPLATES"
UNION ALL SELECT 'PROGRAMS', count(*) FROM "PROGRAMS"
UNION ALL SELECT 'PROGRAM_BLOCKS', count(*) FROM "PROGRAM_BLOCKS"
UNION ALL SELECT 'PROGRAM_SESSIONS', count(*) FROM "PROGRAM_SESSIONS"
UNION ALL SELECT 'PROGRAM_CIRCUITS', count(*) FROM "PROGRAM_CIRCUITS"
UNION ALL SELECT 'PROGRAM_EXERCISES', count(*) FROM "PROGRAM_EXERCISES"
UNION ALL SELECT 'SESSIONS', count(*) FROM "SESSIONS"
UNION ALL SELECT 'SESSION_PARTICIPANTS', count(*) FROM "SESSION_PARTICIPANTS"
UNION ALL SELECT 'SESSION_VOUCHERS', count(*) FROM "SESSION_VOUCHERS"
UNION ALL SELECT 'ORDERS', count(*) FROM "ORDERS"
UNION ALL SELECT 'MEMBER_COACHES', count(*) FROM "MEMBER_COACHES"
UNION ALL SELECT 'MEMBERS', count(*) FROM "MEMBERS"
UNION ALL SELECT 'COACHES', count(*) FROM "COACHES"
UNION ALL SELECT 'USERS', count(*) FROM "USERS"
ORDER BY 1;
```
Colle le résultat dans le suivi de session. C'est le point de retour exigé en
phase 6.

### P0.5 Jeu d'acteurs
Quatre jetons nécessaires. Les OTP sont en clair dans les logs :
`docker compose logs --since 2m api | Select-String -Pattern "OTP"`

| Acteur | Qui | À créer ? |
|---|---|---|
| COACH_A | Federicoach, `admin@web2success.fr`, COACH_ID `cebb9b4d-00ee-4826-a8fe-58e8ab3b325b` | non |
| MEMBER_1 | `member@mentora.fr`, MEMBER_ID `25d5b053-d885-4887-b297-822d4341e7ff`, rattaché à COACH_A | non |
| COACH_B | second coach, à créer via le parcours OTP normal | **oui, fixture** |
| MEMBER_2 | second membre, rattaché à COACH_B uniquement | **oui, fixture** |

COACH_B et MEMBER_2 sont la condition de possibilité de toute la phase 2.
Sans eux, les tests de scope ne prouvent rien. Note leurs UUID, ils servent au
nettoyage de la phase 6.

---

## Phase 1 — Endpoints du lot 5.1, jamais éprouvés sur VPS

Priorité la plus haute : ce code tourne en démo depuis des semaines sans avoir
jamais été appelé en conditions réelles.

| # | Appel | Jeton | Attendu |
|---|---|---|---|
| P1.1 | `GET /api/v1/coach/session-slots` | COACH_A | 200, liste non vide (1085 créneaux seedés), pagination cohérente |
| P1.2 | `GET /api/v1/coach/sessions` | COACH_A | 200, chaque séance porte bien l'identité du membre (nom, pas seulement l'id) |
| P1.3 | `GET /api/v1/coach/conversations` | COACH_A | 200, inbox agrégée, compteurs de non-lus cohérents avec la base |
| P1.4 | `GET /api/v1/coach/members/{MEMBER_1}/conversation` | COACH_A | 200, URL visio permanente présente et en **https** |
| P1.5 | `GET /api/v1/coach/members/{MEMBER_1}/conversation/messages` | COACH_A | 200 |
| P1.6 | `POST /api/v1/coach/members/{MEMBER_1}/conversation/messages` | COACH_A | 201 ou 200, message visible côté membre en P1.7 |
| P1.7 | `PATCH /api/v1/coach/members/{MEMBER_1}/conversation/messages/read` | COACH_A | 200, compteur de non-lus retombé |
| P1.8 | `GET /api/v1/coach/members/{MEMBER_1}/parameters` | COACH_A | 200, get-or-create : la ligne est créée avec ses défauts si absente, jamais d'erreur |
| P1.9 | `PUT /api/v1/coach/members/{MEMBER_1}/parameters` | COACH_A | 200, relecture P1.8 conforme |
| P1.10 | `GET /api/v1/coach/parameters` | COACH_A | 200, `MISSED_SESSION_BEHAVIOR` présent avec `SKIP` par défaut |
| P1.11 | `POST /api/v1/devices` | MEMBER_1 | 200/201 |
| P1.12 | `DELETE /api/v1/devices` | MEMBER_1 | 200/204 |
| P1.13 | `POST /api/v1/account/deletion-request` | MEMBER_2 | 200, marqueur posé |
| P1.14 | `GET /api/v1/account/deletion-request` | MEMBER_2 | 200, statut cohérent |
| P1.15 | `DELETE /api/v1/account/deletion-request` | MEMBER_2 | 200, statut annulé |
| P1.16 | `POST /api/v1/auth/logout` puis `POST /api/v1/auth/token/refresh` avec le refresh révoqué | MEMBER_2 | 401. **Un 200 ici est bloquant.** |

Vérification transverse de la phase : sur la séance ouverte en P1.2, contrôler
la fenêtre visio par séance (l'URL n'est active que dans sa fenêtre horaire).

---

## Phase 2 — Scope multi-tenant : 404 et jamais 403

Toute la phase se joue avec le jeton **COACH_B** sur des ressources de COACH_A,
et avec **MEMBER_2** sur des ressources de MEMBER_1.

Un 403 est un échec. Un 200 est un échec grave. Seul 404 passe.

| # | Appel avec COACH_B sur ressource COACH_A | Attendu |
|---|---|---|
| P2.1 | `GET /coach/exercises/{exercice privé de A}` | 404 |
| P2.2 | `GET /coach/exercises/{fiche Mentora, COACH_ID null}` | **200** (visible de tous, c'est la règle) |
| P2.3 | `PUT /coach/exercises/{fiche Mentora}` | 404 (lecture autorisée, écriture non) |
| P2.4 | `DELETE /coach/exercises/{exercice privé de A}` | 404 |
| P2.5 | `GET /coach/program-templates/{trame de A}` | 404 |
| P2.6 | `PUT /coach/program-templates/{trame de A}` | 404 |
| P2.7 | `DELETE /coach/program-templates/{trame de A}` | 404 |
| P2.8 | `GET /coach/members/{MEMBER_1}/training-programs` | 404 |
| P2.9 | `POST /coach/members/{MEMBER_1}/training-programs` | 404 |
| P2.10 | `GET /coach/training-programs/{programme de A}` | 404 |
| P2.11 | `PUT /coach/training-programs/{programme de A}` | 404 |
| P2.12 | `DELETE /coach/training-programs/{programme de A}` | 404 |
| P2.13 | `PUT /coach/program-sessions/{séance de A}/booking` | 404 |
| P2.14 | `PUT /coach/program-sessions/{séance de A}/completion` | 404 |
| P2.15 | `GET /coach/sessions/{séance de A}/participants` | 404 |
| P2.16 | `POST /coach/sessions/{séance de A}/participants` | 404 |
| P2.17 | `DELETE /coach/sessions/{séance de A}/participants/{MEMBER_1}` | 404 |
| P2.18 | `GET /coach/members/{MEMBER_1}/conversation` (5.1) | 404 |
| P2.19 | `GET /coach/members/{MEMBER_1}/parameters` (5.1) | 404 |
| P2.20 | `POST /coach/sessions/group` avec le `slotId` d'un créneau de A | 404 |
| P2.21 | `POST /coach/sessions/group` avec un `productId` de A | 404 |

| # | Appel avec MEMBER_2 sur ressource MEMBER_1 | Attendu |
|---|---|---|
| P2.22 | `GET /member/programs/{programme de MEMBER_1}` | 404 |
| P2.23 | `PUT /member/program-sessions/{séance de MEMBER_1}/completion` | 404 |
| P2.24 | `GET /member/exercises/{exercice privé de COACH_A}` | 404 (MEMBER_2 n'est pas rattaché à A) |

Croisement de policies, à faire une fois :
- P2.25 : un jeton COACH sur `/api/v1/member/me` → 403 attendu (mauvais rôle,
  pas mauvais scope, 403 est ici correct).
- P2.26 : un jeton MEMBER sur `/api/v1/coach/me` → 403 attendu.

---

## Phase 3 — Agenda fusionné et V_SESSION_MEMBERS

Zone non couverte par la revue statique. À traiter avec attention.

- **P3.1** `GET /coach/agenda` avec COACH_A → 200. Aucune séance d'un autre
  coach dans la réponse. Recouper avec
  `SELECT count(*) FROM "SESSIONS" WHERE "SESSION_COACH_ID" = '{COACH_A}';`
- **P3.2** `GET /coach/agenda` avec COACH_B → 200 avec **uniquement** ses
  propres séances, ou liste vide. Une seule séance de A qui fuit ici est
  bloquante.
- **P3.3** `GET /member/agenda` avec MEMBER_1 → 200, séances de ses coachs
  uniquement.
- **P3.4** Cours collectif : créer une séance de groupe avec COACH_A
  (`POST /coach/sessions/group`), y inscrire MEMBER_1
  (`POST /coach/sessions/{id}/participants`), puis
  `GET /coach/sessions/{id}/participants` → MEMBER_1 présent.
- **P3.5** Lire la même séance via `GET /coach/agenda` et via
  `GET /coach/sessions/{id}` : le membre doit apparaître dans les deux, par le
  chemin de lecture unifié.
- **P3.6** Capacité : inscrire des participants jusqu'à dépasser la capacité
  figée en snapshot sur la séance → refus attendu (409). Puis modifier la
  capacité du `PRODUCTS` source et réessayer : le refus doit persister, la
  séance lit son snapshot, pas le produit vivant.
- **P3.7** Réservation membre sur une offre collective :
  `POST /member/sessions` avec un produit `PRESENTIEL_GROUPE` → refus attendu.
  C'est le coach qui inscrit, définitivement.
- **P3.8** Annulation d'un cours collectif par le coach : noter le
  comportement réel sur les vouchers des participants. Le backlog dit que le
  voucher n'est pas rendu à chaque participant. Confirmer, ne pas corriger.

---

## Phase 4 — Parcours complet

À dérouler d'un bout à l'autre avec COACH_A et MEMBER_1, sans sauter d'étape.

1. **P4.1** Créer une trame : `POST /coach/program-templates` avec un arbre
   Macrocycle > Mésocycle > Microcycle > Séance > Circuit > Exercice, en
   pointant des fiches Mentora. → 201.
2. **P4.2** Relire : `GET /coach/program-templates/{id}` → l'arbre `jsonb`
   revient identique, aucun niveau perdu.
3. **P4.3** Validation stricte : renvoyer la même trame avec un circuit vide,
   puis avec un circuit mêlant champs `STANDARD` et `INTERVAL` → **422** avec
   le dictionnaire `errors` (clés en PascalCase). Un 400 ici est un écart.
4. **P4.4** Trame avec un `exerciseId` appartenant à COACH_B → 422.
5. **P4.5** Assigner : `POST /coach/members/{MEMBER_1}/training-programs` avec
   `templateId` → 201.
6. **P4.6** Copie profonde : modifier la trame source (P4.1), puis relire le
   programme assigné → **aucune propagation**. Vérifier en base que
   `PROGRAM_BLOCKS`, `PROGRAM_SESSIONS`, `PROGRAM_CIRCUITS` et
   `PROGRAM_EXERCISES` ont bien été créés en propre.
7. **P4.7** Fiches d'exercice pointées, jamais copiées :
   `SELECT count(*) FROM "EXERCISES";` doit être inchangé depuis P0.4.
8. **P4.8** Réserver : `POST /member/sessions` avec MEMBER_1 sur un créneau
   correspondant à une séance du programme → 201.
9. **P4.9** Rattachement automatique : relire la séance de programme, le lien
   vers la réservation doit exister sans intervention.
   ```sql
   SELECT "PROGRAM_SESSION_ID", "PROGRAM_SESSION_SESSION_ID"
   FROM "PROGRAM_SESSIONS"
   WHERE "PROGRAM_SESSION_SESSION_ID" IS NOT NULL
   ORDER BY "PROGRAM_SESSION_CREATED_DATE" DESC LIMIT 5;
   ```
10. **P4.10** Idempotence du rattachement : annuler puis re-réserver le même
    créneau. Aucun doublon de lien.
11. **P4.11** Saisie du réalisé côté élève :
    `PUT /member/program-sessions/{id}/completion` → 200.
12. **P4.12** Le prescrit n'est pas écrasé : relire la séance, les colonnes
    prescrites doivent être intactes à côté du réalisé.
13. **P4.13** Saisie du réalisé côté coach :
    `PUT /coach/program-sessions/{id}/completion` → 200, même contrôle.
14. **P4.14** `lastPerformed` : relire le programme, la dernière performance
    de l'exercice concerné doit remonter.
15. **P4.15** `MISSED_SESSION_BEHAVIOR` : passer le paramètre coach à `SHIFT`
    via `PUT /coach/parameters`, relire le programme d'un membre ayant une
    séance manquée, le décalage doit apparaître **en lecture**. Puis en base :
    ```sql
    SELECT "PROGRAM_SESSION_ID", "PROGRAM_SESSION_PLANNED_DATE"
    FROM "PROGRAM_SESSIONS" WHERE "PROGRAM_SESSION_PROGRAM_ID" = '{programId}';
    ```
    **Les dates en base ne doivent pas avoir bougé.** Une mutation persistée
    ici est un défaut réel.
16. **P4.16** Annulation membre : annuler une séance à plus de 24 h → voucher
    rendu. Annuler à moins de 24 h → voucher consommé. Vérifier les statuts en
    base, pas seulement la réponse HTTP.

---

## Phase 5 — Preuves runtime

Le mode Development est actif sur le VPS, les logs SQL sont complets. On s'en
sert.

### P5.1 Absence de N+1 sur la lecture d'arbre
```bash
docker compose logs --since 10s api | grep -c "Executed DbCommand"
```
Vider le buffer, appeler `GET /member/programs/{id}` une fois, recompter.
Attendu : un petit nombre fixe de requêtes, indépendant du nombre de blocs, de
séances et d'exercices. Refaire l'appel sur un programme deux fois plus gros :
le compte ne doit pas doubler.

### P5.2 Clôture de F2 — SQL réel de `ResolveLastPerformedAsync`
Appeler l'endpoint qui remonte `lastPerformed` (P4.14), puis :
```bash
docker compose logs --since 30s api | grep -A 20 "PROGRAM_EXERCISES"
```
Lire le SQL généré. Attendu : une seule requête avec sous-requête corrélée, pas
une requête par exercice. Si c'est une requête par exercice, F2 devient réel et
part en correction.

### P5.3 SQL de scope sur la vue
Appeler `GET /coach/agenda` avec COACH_B, récupérer le SQL dans les logs et
vérifier de visu que le `WHERE` porte bien le `COACH_ID` sur chaque branche de
l'UNION de `V_SESSION_MEMBERS`.

### P5.4 Idempotence des seeders
```sql
SELECT count(*) FROM "EXERCISES" WHERE "EXERCISE_COACH_ID" IS NULL;
```
Attendu : 45. Puis `docker compose restart api`, attendre le health vert, et
recompter. **Toujours 45.** Un 90 signifie un seeder non idempotent, bloquant.

### P5.5 Codes d'erreur du middleware
Sur trois appels au hasard déjà faits : confirmer que l'enveloppe
`{ success, data, error, statusCode }` est respectée, que le 422 porte la clé
`errors` et que le 409 porte la clé `details`.

---

## Phase 6 — Nettoyage et retour à la baseline

Rien ne se merge tant que cette phase n'est pas close avec ses comptages.

1. **P6.1** Supprimer, par les endpoints de l'API et non en SQL direct :
   programmes assignés créés en P4, trames créées en P4.1, séances de groupe et
   participants créés en P3.4, réservations créées en P4.8.
2. **P6.2** Supprimer COACH_B et MEMBER_2 et leurs dépendances. Si aucun
   endpoint ne le permet, faire le SQL ligne à ligne, dans l'ordre des FK, et
   **me le soumettre avant exécution**. Aucune opération destructive en base
   sans validation explicite.
3. **P6.3** Relancer la requête de baseline P0.4 et comparer colonne par
   colonne avec le relevé initial.
4. **P6.4** Tout écart non expliqué est listé nommément. Un écart accepté doit
   être justifié par écrit dans le suivi de session, pas toléré en silence.
5. **P6.5** Contrôle des jetons résiduels :
   ```sql
   SELECT count(*) FROM "REFRESH_TOKENS";
   ```
   Le backlog note 151 jetons sans purge. Noter la nouvelle valeur, ne rien
   supprimer.

---

## Sortie attendue de la passe

Un relevé unique avec, pour chaque point : identifiant, résultat réel, écart
éventuel. Les écarts sont ensuite triés en deux piles, corrections avant merge
et backlog, comme pour l'ultrareview. Ensuite seulement : passe de correction
unique, second déploiement, merge.
