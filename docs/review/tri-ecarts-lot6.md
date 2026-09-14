# Tri des écarts — passe de tests VPS lot 5.1 + lot 6

Passe exécutée le 14/09/2026 sur `api-demo.mentorapp.fr`, branche `feature/lot_6`
(6be3cf6). Environ 70 vérifications, phase 5 en SSH, retour à la baseline confirmé.

---

## À corriger avant le merge

### C1. Validation d'homogénéité du circuit inopérante (P4.3b)
**Constat :** une trame dont un circuit mêle des champs `STANDARD` et `INTERVAL`
est acceptée en 200. Attendu : 422.
**Pourquoi c'est bloquant :** c'est une règle produit actée (un circuit est
homogène, le mode décide de ce que le mobile affiche : compteur de répétitions
ou chronomètre). Un circuit incohérent en base donne un affichage indéterminé
côté Flutter, sur une donnée déjà copiée en profondeur dans tous les programmes
assignés.
**À noter :** P4.3a (circuit vide) renvoie bien 422, donc le validateur tourne.
C'est cette règle précise qui ne mord pas.
**Vérification après correction :** rejouer P4.3a et P4.3b, les deux doivent
renvoyer 422 avec la clé `errors`.

### C2. Validation du modèle avant résolution du scope (P2.3, P2.6, P2.11, F5)
**Constat :** sur les `PUT` et certains `POST`, un corps invalide produit un 400
`ProblemDetails` natif d'ASP.NET, hors enveloppe `{ success, data, error,
statusCode }`, avec un `traceId` exposé. Exemples relevés :
- `PUT /coach/exercises/{id}` avec COACH_B → 400 au lieu de 404
- `PUT /coach/program-templates/{id}` avec COACH_B → 400 au lieu de 404
- `PUT /coach/training-programs/{id}` avec COACH_B → 422 au lieu de 404
- `PUT /coach/program-sessions/{id}/completion` sans `exercises` → 400 natif

**Cause unique :** `[ApiController]` déclenche la validation automatique du
modèle avant d'entrer dans le service. Ni FluentValidation ni le contrôle de
propriété ne s'exécutent.
**Pourquoi c'est bloquant :** un coach étranger distingue « corps invalide » de
« ressource inexistante », ce qui contredit la règle 404-jamais-403. Et
l'enveloppe standard saute sur toute une famille d'endpoints, ce qui casse le
contrat côté PJ.
**Piste :** `SuppressModelStateInvalidFilter = true` dans la configuration
`ApiBehaviorOptions`, pour que les corps invalides retombent sur FluentValidation
dans les services. À valider soigneusement, ça touche tous les controllers.
**Vérification après correction :** rejouer P2.3, P2.6, P2.11 (404 attendu) et
un `PUT` à corps invalide sur une ressource possédée (422 + enveloppe attendus).

### C3. 403 au lieu de 404 sur la conversation coach (P2.18)
**Constat :** `GET /coach/members/{memberId}/conversation` avec un coach non
rattaché renvoie 403.
**Pourquoi c'est bloquant :** violation directe de la règle. Et c'est incohérent
dans la même famille, `/parameters` renvoie bien 404 sur le même memberId.
**Code concerné :** lot 5.x, jamais éprouvé sur VPS avant cette passe.
**Vérification après correction :** P2.18 doit renvoyer 404.

### C4. Corrections documentaires (F4, F6)
Commit doc séparé du commit fonctionnel.
- `ProductRequest.OfferType` : le commentaire XML affirme que
  `PRESENTIEL_GROUPE` est rejeté, alors qu'il est requis pour les produits de
  cours collectif.
- `AgendaService.cs` : commentaire « until Lot 6.6 ships », 6.6 est dans la même
  branche.

---

## Backlog

### B1. `Down()` de la migration `Lot6_4_GroupSessions` (F1)
Le `Down()` repasse `SESSION_MEMBER_ID`, `SESSION_VOUCHER_ID` et
`SESSION_PRODUCT_ID` en `NOT NULL` avec un `defaultValue` GUID vide, sans
backfill. Échoue dès qu'un cours collectif existe. Sans impact : merge
fast-forward, aucun rollback prévu, et le filet réel est le `pg_dump`.

### B2. 400 au lieu de 401 sur refresh révoqué (P1.16)
Après logout, le refresh token est bien rejeté, la sécurité tient. Seul le code
HTTP est faux.

### B3. 200 au lieu de 201 sur `POST /member/sessions` (P4.8)
Création de ressource sans 201. Cosmétique.

### B4. Enums en PascalCase sur `session-slots`
`POST /coach/session-slots` exige `PresentielGroupe`, alors que le reste de l'API
travaille en `PRESENTIEL_GROUPE`. Deux formats pour le même enum selon
l'endpoint. Lot 5.x. À unifier avec la dette de casing déjà connue.

### B5. Schémas de réponse vides dans la Swagger
Presque tous les endpoints exposent `schema: {}` en réponse. PJ code contre la
doc à l'aveugle. Repéré pendant la passe énumérative.

### B6. Nommage incohérent sur `/coach/programs`
Renvoie `id` et `createdAt`, là où le reste de l'API utilise `<entité>Id` et
`createdDate`.

### B7. Endpoints Stripe internes sans authentification
`POST /internal/stripe/simulate-webhook` et
`GET /internal/stripe/fake-checkout/{sessionId}` sont en `policy: none`. Sur un
VPS en Development avec Swagger public, n'importe qui peut se créditer des
vouchers. **Prérequis de la bêta fermée**, au même titre que la fermeture du
port 5432.

### B8. Restitution des vouchers à l'annulation d'un cours collectif
Déjà au backlog. L'API refuse explicitement l'annulation avec un message clair,
donc le comportement est assumé, pas cassé.

---

## Clos, aucune action

- **F2** (`ResolveLastPerformedAsync`) : le SQL réel montre une seule requête
  avec `LEFT JOIN LATERAL` et `LIMIT 1`, 4 ms. La remontée était infondée.
- **F3** (CHECK sur `SESSIONS`) : `CK_SESSIONS_GROUP_HAS_NO_MEMBER` est en place
  et validé par Postgres sur les données réelles au moment de la migration.
- **P4.15** (SHIFT non persisté) : `PROGRAM_SESSIONS` ne contient aucune colonne
  de date. Le décalage est structurellement calculé à la lecture. La
  vérification n'avait pas lieu d'être.
- **P3.8** (annulation de cours collectif) : refus volontaire et documenté dans
  le code.
- **P4.16** (fenêtre d'annulation) : voucher `CONSUMED` à 21 h de la séance,
  fenêtre de 24 h. Comportement correct. La restitution au-delà de la fenêtre
  reste non testée.

---

## Ce que la passe a prouvé

- **Cloisonnement multi-tenant :** une vingtaine de vérifications croisées, 404
  partout. Un coach ne lit ni ne modifie les données d'un autre, un élève non
  plus. Couvre exercices, trames, programmes, séances de programme, participants
  et séances de groupe.
- **Parcours complet du lot 6 :** trame, assignation, copie profonde (arbre
  complet en propre, fiches d'exercice pointées et jamais copiées), saisie du
  réalisé côté coach et côté élève, prescrit jamais écrasé, `coachNote` fournie
  par un élève correctement ignorée.
- **Performance :** 7 requêtes pour lire un programme complet, pas de N+1, pas de
  dégradation avec la taille de l'arbre.
- **Seeders idempotents :** 45 fiches Mentora avant et après redémarrage.
- **Scope prouvé par le SQL réel :** agenda coach filtré sur `@__coachId_0` sur
  les trois requêtes, agenda membre filtré via `V_SESSION_MEMBERS` sur
  `MEMBER_ID`.
- **Archivage automatique :** l'assignation d'un nouveau programme archive le
  précédent, la règle « un seul programme actif par élève » tient.

---

## Fixtures conservées en base pour la seconde passe

| Élément | Id | Note |
|---|---|---|
| COACH_B | `b0000000-0000-4000-8000-000000000002` | `coach2@mentorapp.fr` |
| USER de COACH_B | `b0000000-0000-4000-8000-000000000001` | |
| MEMBER_2 | `b0000000-0000-4000-8000-000000000004` | `member2@mentorapp.fr`, non rattaché |
| USER de MEMBER_2 | `b0000000-0000-4000-8000-000000000003` | |

Créés en SQL direct : l'inscription libre n'existe pas encore (lot marketplace).
MEMBER_2 doit rester non rattaché, c'est ce qui valide P2.22 à P2.24.

Boîtes IONOS `coach2@mentorapp.fr` et `member2@mentorapp.fr` conservées.
Le sous-adressage `+` ne fonctionne pas chez IONOS (rejet 550).

---

## Suite

1. Passe de correction : C1, C2, C3, puis C4 en commit doc séparé.
2. Build vert.
3. Redéploiement VPS (`pg_dump` d'abord, séquence fixe).
4. Rejeu de la passe : `docs/review/run-vps-tests.ps1`, plus les vérifications
   manuelles de C1, C2 et C3.
5. `git push origin feature/lot_6` (les 7 commits sont encore locaux).
6. Merge fast-forward via GitHub, par François.
7. Mail à PJ.
8. Montée `System.IdentityModel.Tokens.Jwt` 7.0.3 vers 8.x, étape isolée.
9. Lot 7, visio LiveKit.
