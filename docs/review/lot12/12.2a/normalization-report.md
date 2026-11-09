# Exercise catalog: normalization report (lot 12.2a)

For the product owner. The Mentora exercise catalog (275 exercises) used free text for six descriptive fields. These fields are now closed lists: each exercise points to one entry of a fixed list, and the app shows that entry's label. This report shows how the existing values were sorted into the lists, which values could not be placed, and what needs a decision. Source: local database after the one-time normalization (identical on a freshly built database).

## 1. Totals

| Field | Exercises with a value | Placed in the list | Not placed (old text kept) |
|---|---|---|---|
| Category | 248 | 248 | 0 |
| Type | 248 | 246 | 2 |
| Laterality | 248 | 248 | 0 |
| Movement pattern | 248 | 218 | 30 |
| Starting position | 248 | 238 | 10 |
| Grip type | 160 | 160 | 0 |

- The 27 historical exercises (codes LG..) have none of these six fields filled (section 5).
- 88 catalog exercises have no grip type (empty in the sheet).
- Secondary muscles: 219 exercises had a list of secondary muscles. They are now linked to the 14 muscle groups of the app: 381 links. One value, "jambes", could not be placed (section 4).

When a value could not be placed, the exercise keeps its original text, so nothing is lost. These exercises are shown in sections 3 and 4 so that you can pick the right entry, or ask for a new entry, in the admin tool.

## 2. Each list with its number of exercises

**Category**

| Label | Exercises |
|---|---|
| Musculation | 186 |
| Gainage | 34 |
| Cardio | 20 |
| Conditionnement | 8 |

**Type**

| Label | Exercises |
|---|---|
| Polyarticulaire | 122 |
| Monoarticulaire | 74 |
| Isolation | 11 |
| Isométrique | 7 |
| Dynamique | 8 |
| Pliométrique | 4 |
| Cardio | 18 |
| Conditionnement | 2 |

**Laterality**

| Label | Exercises |
|---|---|
| Bilatéral | 177 |
| Unilatéral | 55 |
| Alterné | 14 |
| Diagonal | 2 |

**Movement pattern**

| Label | Exercises |
|---|---|
| Poussée horizontale | 24 |
| Poussée verticale | 11 |
| Tirage horizontal | 17 |
| Tirage vertical | 10 |
| Squat | 8 |
| Charnière de hanche | 6 |
| Fente | 5 |
| Extension de hanche | 15 |
| Flexion du genou | 6 |
| Extension du genou | 3 |
| Flexion du coude | 15 |
| Extension du coude | 9 |
| Abduction | 18 |
| Adduction | 9 |
| Rotation d’épaule | 5 |
| Flexion du tronc | 8 |
| Anti-extension | 7 |
| Anti-rotation | 4 |
| Anti-inclinaison | 4 |
| Flexion plantaire | 4 |
| Locomotion | 19 |
| Porté | 6 |
| Saut | 5 |

**Starting position**

| Label | Exercises |
|---|---|
| Debout | 102 |
| Assis | 44 |
| Allongé sur le dos | 16 |
| Allongé sur le ventre | 6 |
| Allongé sur le côté | 3 |
| Allongé sur banc | 12 |
| Banc incliné | 5 |
| Banc décliné | 2 |
| Buste penché | 9 |
| Position de planche | 12 |
| Appui sur les avant-bras | 6 |
| Appui sur banc | 4 |
| À quatre pattes | 6 |
| À genoux | 3 |
| Suspendu | 8 |

**Grip type**

| Label | Exercises |
|---|---|
| Sans objet | 0 |
| Neutre | 85 |
| Pronation | 31 |
| Supination | 16 |
| Pronation large | 1 |
| Pronation serrée | 3 |
| Mixte | 1 |
| Variable | 23 |

## 3. Values not placed (decision needed)

**Type (2 exercises)**

| Original value | Count | Exercises |
|---|---|---|
| Isométrique / dynamique | 2 | EX207 Pallof press; EX208 Pallof press avec élastique |

**Movement pattern (30 exercises)**

| Original value | Count | Exercises |
|---|---|---|
| Descente contrôlée | 1 | EX149 Step-down |
| Dorsiflexion cheville | 1 | EX167 Tibial raise |
| Élévation diagonale | 1 | EX094 Y raise haltères |
| Élévation scapulaire | 2 | EX071 Shrug haltères; EX072 Shrug barre |
| Extension d'épaule | 2 | EX066 Pull-over haltère; EX067 Pull-over machine |
| Extension du poignet | 1 | EX130 Extension poignets inversée |
| Extension du tronc | 1 | EX074 Superman au sol |
| Flexion de hanche | 1 | EX199 Relevé de genoux chaise romaine |
| Flexion de hanche / bassin | 2 | EX197 Relevé de genoux suspendu; EX198 Relevé de jambes suspendu |
| Flexion du poignet | 2 | EX129 Extension poignets barre; EX131 Curl poignets haltères |
| Flexion épaule | 4 | EX090 Élévation frontale haltères; EX091 Élévation frontale alternée haltères; EX092 Élévation frontale barre; EX093 Élévation frontale poulie |
| Gainage dynamique | 1 | EX219 Hollow rocks |
| Inclinaison latérale | 1 | EX223 Heel touches |
| Isométrique | 1 | EX220 L-sit |
| Mouvement cyclique | 1 | EX233 Elliptique |
| Ondulation alternée | 1 | EX237 Battle rope vagues alternées |
| Ondulation simultanée | 1 | EX238 Battle rope vagues simultanées |
| Poussée diagonale | 2 | EX084 Landmine press unilatéral; EX085 Landmine press à deux mains |
| Pronation / supination | 1 | EX132 Pronation supination haltère |
| Rotation contrôlée | 2 | EX209 Wood chop poulie haute vers basse; EX210 Wood chop poulie basse vers haute |
| Rotation du tronc | 1 | EX211 Russian twist |

**Starting position (10 exercises)**

| Original value | Count | Exercises |
|---|---|---|
| Appui mains | 1 | EX213 Planche haute |
| Assis ou debout | 1 | EX120 Extension triceps au-dessus de la tête haltère |
| Au sol | 1 | EX011 Planche |
| Banc à 45 degrés | 1 | EX191 Extension hanche sur banc à 45 degrés orientée fessiers |
| Banc à lombaires | 1 | EX073 Extension lombaire au banc |
| Incliné | 2 | EX064 Tirage TRX; EX088 Élévation latérale inclinée haltère |
| Incliné sous anneaux | 1 | EX063 Rowing inversé aux anneaux |
| Machine | 1 | EX139 Hack squat |
| Machine assistée | 1 | EX119 Dips assistés |

## 4. Secondary muscle values dropped

| Original value | Count | Exercises |
|---|---|---|
| jambes | 6 | EX013 Rameur; EX133 Farmer carry; EX225 Front rack carry; EX228 SkiErg; EX240 Sled pull; EX242 Farmer carry lourd |

"jambes" covers several muscle groups (quadriceps, ischio-jambiers, fessiers, mollets), so it was not linked. These six exercises keep their other secondary muscles. A muscle group that is already the exercise's primary group is not repeated as a secondary one.

## 5. Historical exercises without category

These 27 exercises predate the catalog sheet and have no category, type, laterality, movement pattern, starting position or grip type.

| Code | Name |
|---|---|
| LG03 | Écarté couché haltères |
| LG07 | Traction / tirage vertical |
| LG10 | Tirage horizontal poulie basse |
| LG11 | Développé épaules barre |
| LG13 | Élévations frontales haltères |
| LG14 | Oiseau (élévations arrière) |
| LG15 | Curl biceps barre |
| LG16 | Curl biceps haltères alterné |
| LG17 | Curl marteau haltères |
| LG18 | Dips |
| LG19 | Extension triceps poulie haute |
| LG21 | Curl poignet barre |
| LG22 | Extension poignet haltère |
| LG23 | Suspension à la barre |
| LG24 | Crunch |
| LG25 | Gainage planche |
| LG27 | Rotation russe |
| LG28 | Squat |
| LG29 | Fentes avant |
| LG33 | Soulevé de terre jambes tendues |
| LG36 | Fentes bulgares |
| LG37 | Kickback à la poulie |
| LG38 | Mollets debout |
| LG40 | Burpees |
| LG41 | Kettlebell swing |
| LG42 | Thruster haltères |
| LG44 | Course à pied (tapis) |

## 6. Historical exercises replaced by a catalog exercise

These 18 historical exercises were merged at lot 9 into the catalog exercise of the same name. They are never created again while the catalog exercise exists.

| Historical code | Catalog code | Name |
|---|---|---|
| LG01 | EX001 | Développé couché barre |
| LG02 | EX016 | Développé couché haltères |
| LG04 | EX029 | Pompes |
| LG05 | EX018 | Développé incliné haltères |
| LG06 | EX003 | Soulevé de terre |
| LG08 | EX004 | Rowing barre buste penché |
| LG09 | EX052 | Rowing haltère unilatéral |
| LG12 | EX010 | Élévations latérales haltères |
| LG20 | EX124 | Barre au front |
| LG26 | EX198 | Relevé de jambes suspendu |
| LG30 | EX140 | Presse à cuisses |
| LG31 | EX142 | Leg extension |
| LG32 | EX155 | Leg curl allongé |
| LG34 | EX154 | Good morning barre |
| LG35 | EX007 | Hip thrust barre |
| LG39 | EX164 | Mollets assis |
| LG43 | EX235 | Corde à sauter |
| LG45 | EX013 | Rameur |

## 7. Not in the catalog

- EX135: two lines of the sheet share this code ("Dead hang" and "Front squat"), so neither was imported.
- EX202 "Dead bug": the line is shifted by one column in the sheet and is not marked active, so it was not imported.
