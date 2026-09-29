# Lot 9.3 — Exercise catalog import: runtime evidence

Local only (`mentora-postgres-local`, port 5433; API on 5243). No endpoint, DTO, service or validator change. Seeder: `Mentora.Infrastructure/Seeding/CatalogSeeder.cs`, invoked right after `MuscleGroupSeeder`.

## Preflight
- Branch `feature/lot_9`, HEAD `e9a6138`, only `Mentora.Infrastructure/Seeding/Catalog/` untracked; `dotnet build`: 19 warnings, 0 errors.
- Headers of the four CSV files identical to the spec. All four valid UTF-8 (`iconv` round-trip), no BOM (first bytes `65 78 65 72` / `6d 61 74 65` / `6d 75 73 63` / `65 78 65 72`); no field contains a line break.
- Data rows (CSV-parsed): EXERCICES 251 (250 `actif = Oui`, EX202 Dead bug blank), MATERIELS 70 (all Oui), MUSCLES 50 (`actif` blank on every row; not filtered, the spec does not filter muscles), EXERCICES_MUSCLES 1721.
- Baseline: `ex 45 | eq 0 | obj 0 | exobj 0 | mg 14`.
- Package: CsvHelper 33.1.0; `dotnet list package --vulnerable --include-transitive` does not list it (the two listed advisories, Microsoft.Extensions.Caching.Memory 8.0.0 and Npgsql 8.0.0, pre-exist via EF Core / Npgsql 8.0.0).

## Product-owner decisions taken during the session
1. `EX135` is used by two active rows (Dead hang, Front squat) whose EXERCICES_MUSCLES rows are merged: any `exercise_id` used by several active rows is skipped entirely with a warning.
2. Legacy `EXERCISE_EQUIPMENT` for a new exercise whose `materiel_principal` matches no EQUIPMENT row: the 3.1 keyword mapping is applied to the raw text, plus `aucun` → BODYWEIGHT; else OTHER.
3. Equipment FK match: exact (trim, lowercase) first, then ignoring one trailing `s` per word; plural-only matches are listed separately (V6).

## V1 — first start
```
MuscleGroupSeeder: created 0 muscle group(s).
CatalogSeeder equipment: created 70, existing 0, skipped 0, warnings 0.
CatalogSeeder objectives: created 53, existing 0, skipped 0, warnings 0.
CatalogSeeder primary groups: from muscles 248, from label 0, unresolved 0, mismatches 138, warnings 0.
CatalogSeeder exercises: created 230, adopted 18, already imported 0, skipped 2, objective links added 561, plural-only equipment matches 1, warnings 53.
[CATALOG] exercise EX135: source code used by 2 active rows ('Dead hang', 'Front squat'), all skipped
[CATALOG] unmatched equipment 'Anneaux gymnastique' x1, left NULL: EX063 (primary)
[CATALOG] unmatched equipment 'Aucun' x3, left NULL: EX014 (primary), EX247 (primary), EX248 (primary)
[CATALOG] unmatched equipment 'Banc' x12, left NULL: EX001 (secondary), EX007 (secondary), EX052 (secondary), EX066 (secondary), EX076 (secondary), EX124 (secondary), EX125 (secondary), EX126 (secondary), EX128 (secondary), EX144 (secondary), EX175 (secondary), EX192 (secondary)
[CATALOG] unmatched equipment 'Banc lombaire' x2, left NULL: EX073 (primary), EX191 (primary)
[CATALOG] unmatched equipment 'Banc ou machine reverse hyper' x1, left NULL: EX190 (primary)
[CATALOG] unmatched equipment 'Banc ou tapis' x1, left NULL: EX089 (secondary)
[CATALOG] unmatched equipment 'Banc pupitre' x2, left NULL: EX110 (secondary), EX111 (secondary)
[CATALOG] unmatched equipment 'Barre' x6, left NULL: EX092 (primary), EX112 (secondary), EX116 (primary), EX123 (secondary), EX129 (primary), EX130 (primary)
[CATALOG] unmatched equipment 'Barre fixe' x1, left NULL: EX062 (primary)
[CATALOG] unmatched equipment 'Barre large' x1, left NULL: EX055 (secondary)
[CATALOG] unmatched equipment 'Barre longue' x2, left NULL: EX045 (secondary), EX047 (secondary)
[CATALOG] unmatched equipment 'Barre olympique ou haltère' x1, left NULL: EX172 (primary)
[CATALOG] unmatched equipment 'Barre ou corde' x1, left NULL: EX065 (secondary)
[CATALOG] unmatched equipment 'Barres parallèles ou parallettes' x1, left NULL: EX220 (primary)
[CATALOG] unmatched equipment 'Chaise romaine' x1, left NULL: EX199 (primary)
[CATALOG] unmatched equipment 'Corde' x4, left NULL: EX068 (secondary), EX121 (secondary), EX189 (secondary), EX195 (secondary)
[CATALOG] unmatched equipment 'Corde / barre' x1, left NULL: EX009 (secondary)
[CATALOG] unmatched equipment 'Corde ou sangles' x1, left NULL: EX240 (secondary)
[CATALOG] unmatched equipment 'Disque ou haltère' x1, left NULL: EX194 (primary)
[CATALOG] unmatched equipment 'Escaliers' x1, left NULL: EX249 (primary)
[CATALOG] unmatched equipment 'Haltère ou kettlebell' x3, left NULL: EX134 (primary), EX153 (primary), EX224 (primary)
[CATALOG] unmatched equipment 'Haltères ou kettlebells' x3, left NULL: EX133 (primary), EX226 (primary), EX242 (primary)
[CATALOG] unmatched equipment 'Hip thrust machine' x1, left NULL: EX174 (primary)
[CATALOG] unmatched equipment 'Kettlebell ou haltère' x1, left NULL: EX136 (primary)
[CATALOG] unmatched equipment 'Kettlebells ou haltères' x1, left NULL: EX225 (primary)
[CATALOG] unmatched equipment 'Landmine' x3, left NULL: EX059 (secondary), EX084 (secondary), EX085 (secondary)
[CATALOG] unmatched equipment 'Leg curl' x1, left NULL: EX157 (primary)
[CATALOG] unmatched equipment 'Machine mollets' x1, left NULL: EX163 (primary)
[CATALOG] unmatched equipment 'Machine mollets assis' x1, left NULL: EX164 (primary)
[CATALOG] unmatched equipment 'Machine pullover' x1, left NULL: EX067 (primary)
[CATALOG] unmatched equipment 'Machine à dips assistés' x1, left NULL: EX119 (primary)
[CATALOG] unmatched equipment 'Machine à tractions assistées' x1, left NULL: EX051 (primary)
[CATALOG] unmatched equipment 'Machine élévations latérales' x1, left NULL: EX087 (primary)
[CATALOG] unmatched equipment 'Mur' x1, left NULL: EX167 (secondary)
[CATALOG] unmatched equipment 'Poids du corps' x36, left NULL: EX006 (primary), EX011 (primary), EX012 (primary), EX074 (primary), EX145 (primary), EX146 (primary), EX147 (primary), EX166 (primary), EX167 (primary), EX168 (primary), EX170 (primary), EX171 (primary), EX173 (primary), EX175 (primary), EX176 (primary), EX179 (primary), EX180 (primary), EX184 (primary), EX193 (primary), EX196 (primary), EX200 (primary), EX201 (primary), EX203 (primary), EX204 (primary), EX205 (primary), EX206 (primary), EX212 (primary), EX213 (primary), EX214 (primary), EX218 (primary), EX219 (primary), EX221 (primary), EX222 (primary), EX223 (primary), EX244 (primary), EX245 (primary)
[CATALOG] unmatched equipment 'Poids du corps ou medicine ball' x1, left NULL: EX211 (primary)
[CATALOG] unmatched equipment 'Poignée' x2, left NULL: EX209 (secondary), EX210 (secondary)
[CATALOG] unmatched equipment 'Poignée neutre' x2, left NULL: EX046 (secondary), EX054 (secondary)
[CATALOG] unmatched equipment 'Poignée simple' x12, left NULL: EX044 (secondary), EX048 (secondary), EX056 (secondary), EX086 (secondary), EX093 (secondary), EX098 (secondary), EX099 (secondary), EX113 (secondary), EX114 (secondary), EX122 (secondary), EX127 (secondary), EX207 (secondary)
[CATALOG] unmatched equipment 'Poignées parallèles' x1, left NULL: EX050 (secondary)
[CATALOG] unmatched equipment 'Rack' x2, left NULL: EX002 (secondary), EX154 (secondary)
[CATALOG] unmatched equipment 'Roue abdominale' x1, left NULL: EX216 (primary)
[CATALOG] unmatched equipment 'Sangle cheville' x3, left NULL: EX162 (secondary), EX177 (secondary), EX182 (secondary)
[CATALOG] unmatched equipment 'Sangles' x1, left NULL: EX241 (secondary)
[CATALOG] unmatched equipment 'Sans matériel' x5, left NULL: EX029 (primary), EX030 (primary), EX033 (primary), EX034 (primary), EX037 (primary)
[CATALOG] unmatched equipment 'Sliders' x1, left NULL: EX160 (primary)
[CATALOG] unmatched equipment 'Sliders ou TRX' x1, left NULL: EX215 (primary)
[CATALOG] unmatched equipment 'Support de chevilles' x1, left NULL: EX158 (primary)
[CATALOG] unmatched equipment 'Support fixe' x4, left NULL: EX088 (secondary), EX150 (primary), EX178 (secondary), EX208 (secondary)
[CATALOG] unmatched equipment 'T-bar' x1, left NULL: EX058 (primary)
[CATALOG] unmatched equipment 'Vélo' x1, left NULL: EX015 (primary)
[CATALOG] unmatched equipment 'Vélo spinning' x1, left NULL: EX232 (primary)
[CATALOG] plural-only equipment match 'Haltère' -> 'Haltères' x12: EX052, EX066, EX079, EX088, EX089, EX109, EX111, EX120, EX126, EX132, EX147, EX166
```
<details><summary>The 138 <code>[CATALOG] group mismatch</code> lines of V1, verbatim (tabulated in V5)</summary>

```
[CATALOG] group mismatch EX003 'Soulevé de terre': sheet label 'Chaîne postérieure', derived 'Fessiers'
[CATALOG] group mismatch EX004 'Rowing barre buste penché': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX005 'Tractions pronation': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX008 'Curl haltères': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX009 'Extension triceps à la poulie': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX010 'Élévations latérales haltères': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX011 'Planche': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX012 'Burpee': sheet label 'Corps entier', derived 'Quadriceps'
[CATALOG] group mismatch EX013 'Rameur': sheet label 'Corps entier', derived 'Quadriceps'
[CATALOG] group mismatch EX014 'Course à pied': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX015 'Vélo stationnaire': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX045 'Tirage vertical poulie haute': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX046 'Tirage vertical prise neutre': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX047 'Tirage vertical prise supination': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX048 'Tirage vertical unilatéral': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX049 'Tractions supination': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX050 'Tractions prise neutre': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX051 'Tractions assistées': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX052 'Rowing haltère unilatéral': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX053 'Rowing haltères buste penché': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX054 'Rowing assis poulie basse': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX055 'Rowing assis prise large': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX056 'Rowing poulie basse unilatéral': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX057 'Rowing poitrine appuyée haltères': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX058 'Rowing T-bar': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX059 'Rowing landmine unilatéral': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX060 'Rowing machine assise': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX061 'Rowing machine unilatéral': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX062 'Rowing inversé': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX063 'Rowing inversé aux anneaux': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX064 'Tirage TRX': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX065 'Pull-over poulie haute bras tendus': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX066 'Pull-over haltère': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX067 'Pull-over machine': sheet label 'Dorsaux', derived 'Dos'
[CATALOG] group mismatch EX068 'Face pull': sheet label 'Trapèzes', derived 'Dos'
[CATALOG] group mismatch EX069 'Reverse fly poulie': sheet label 'Trapèzes', derived 'Dos'
[CATALOG] group mismatch EX070 'Reverse pec deck': sheet label 'Trapèzes', derived 'Dos'
[CATALOG] group mismatch EX071 'Shrug haltères': sheet label 'Trapèzes', derived 'Dos'
[CATALOG] group mismatch EX072 'Shrug barre': sheet label 'Trapèzes', derived 'Dos'
[CATALOG] group mismatch EX073 'Extension lombaire au banc': sheet label 'Lombaires', derived 'Dos'
[CATALOG] group mismatch EX074 'Superman au sol': sheet label 'Lombaires', derived 'Dos'
[CATALOG] group mismatch EX075 'Développé militaire barre debout': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX076 'Développé militaire barre assis': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX077 'Développé épaules haltères assis': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX078 'Développé épaules haltères debout': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX079 'Développé épaules unilatéral haltère': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX080 'Arnold press': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX081 'Shoulder press machine': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX082 'Shoulder press machine unilatérale': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX083 'Smith machine développé épaules': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX084 'Landmine press unilatéral': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX085 'Landmine press à deux mains': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX086 'Élévation latérale à la poulie unilatérale': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX087 'Élévation latérale machine': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX088 'Élévation latérale inclinée haltère': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX089 'Élévation latérale couchée sur le côté': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX090 'Élévation frontale haltères': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX091 'Élévation frontale alternée haltères': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX092 'Élévation frontale barre': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX093 'Élévation frontale poulie': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX094 'Y raise haltères': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX095 'Oiseau haltères buste penché': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX096 'Oiseau haltères sur banc incliné': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX097 'Rowing coudes ouverts haltères': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX098 'Rotation externe à la poulie': sheet label 'Coiffe des rotateurs', derived 'Épaules'
[CATALOG] group mismatch EX099 'Rotation interne à la poulie': sheet label 'Coiffe des rotateurs', derived 'Épaules'
[CATALOG] group mismatch EX100 'Rotation externe avec élastique': sheet label 'Coiffe des rotateurs', derived 'Épaules'
[CATALOG] group mismatch EX101 'Rotation interne avec élastique': sheet label 'Coiffe des rotateurs', derived 'Épaules'
[CATALOG] group mismatch EX102 'Scaption haltères': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX103 'Cuban press haltères': sheet label 'Deltoïdes', derived 'Épaules'
[CATALOG] group mismatch EX104 'Curl barre': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX105 'Curl barre EZ': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX106 'Curl incliné haltères': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX107 'Curl marteau': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX108 'Curl marteau alterné': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX109 'Curl concentration': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX110 'Curl pupitre barre EZ': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX111 'Curl pupitre haltère': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX112 'Curl poulie basse': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX113 'Curl poulie unilatéral': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX114 'Curl Bayesian': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX115 'Curl spider': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX116 'Curl inversé barre': sheet label 'Brachio-radial', derived 'Bras'
[CATALOG] group mismatch EX117 'Curl Zottman': sheet label 'Biceps', derived 'Bras'
[CATALOG] group mismatch EX118 'Dips triceps': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX119 'Dips assistés': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX120 'Extension triceps au-dessus de la tête haltère': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX121 'Extension triceps au-dessus de la tête corde': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX122 'Extension triceps poulie unilatérale': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX123 'Extension triceps poulie prise inversée': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX124 'Barre au front': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX125 'Extension triceps haltères couchée': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX126 'Kickback triceps haltère': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX127 'Kickback triceps poulie': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX128 'Développé couché prise serrée triceps': sheet label 'Triceps', derived 'Bras'
[CATALOG] group mismatch EX129 'Extension poignets barre': sheet label 'Fléchisseurs avant-bras', derived 'Avant-bras'
[CATALOG] group mismatch EX130 'Extension poignets inversée': sheet label 'Extenseurs avant-bras', derived 'Avant-bras'
[CATALOG] group mismatch EX131 'Curl poignets haltères': sheet label 'Fléchisseurs avant-bras', derived 'Avant-bras'
[CATALOG] group mismatch EX167 'Tibial raise': sheet label 'Tibial antérieur', derived 'Tibial'
[CATALOG] group mismatch EX203 'Bird dog': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX204 'Planche latérale': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX205 'Planche latérale sur genoux': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX206 'Planche latérale avec abduction': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX207 'Pallof press': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX208 'Pallof press avec élastique': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX209 'Wood chop poulie haute vers basse': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX210 'Wood chop poulie basse vers haute': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX211 'Russian twist': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX212 'Mountain climber': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX213 'Planche haute': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX214 'Planche avec toucher d'épaule': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX215 'Planche avec déplacement avant-arrière': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX216 'Ab wheel rollout': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX217 'Stir the pot': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX223 'Heel touches': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX224 'Suitcase hold': sheet label 'Obliques', derived 'Abdominaux'
[CATALOG] group mismatch EX225 'Front rack carry': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX226 'Overhead carry': sheet label 'Centre du corps', derived 'Abdominaux'
[CATALOG] group mismatch EX227 'Air bike': sheet label 'Corps entier', derived 'Quadriceps'
[CATALOG] group mismatch EX228 'SkiErg': sheet label 'Corps entier', derived 'Quadriceps'
[CATALOG] group mismatch EX229 'Tapis de course': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX230 'Marche inclinée': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX231 'Sprint sur tapis': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX232 'Vélo spinning': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX233 'Elliptique': sheet label 'Corps entier', derived 'Abdominaux'
[CATALOG] group mismatch EX234 'Stair climber': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX237 'Battle rope vagues alternées': sheet label 'Corps entier', derived 'Abdominaux'
[CATALOG] group mismatch EX238 'Battle rope vagues simultanées': sheet label 'Corps entier', derived 'Abdominaux'
[CATALOG] group mismatch EX239 'Sled push': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX240 'Sled pull': sheet label 'Corps entier', derived 'Abdominaux'
[CATALOG] group mismatch EX243 'Sandbag carry': sheet label 'Corps entier', derived 'Abdominaux'
[CATALOG] group mismatch EX244 'Bear crawl': sheet label 'Corps entier', derived 'Abdominaux'
[CATALOG] group mismatch EX245 'Crab walk': sheet label 'Corps entier', derived 'Abdominaux'
[CATALOG] group mismatch EX246 'Shuttle run': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX247 'Sprint extérieur': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX248 'Marche rapide': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX249 'Montée d'escaliers': sheet label 'Membres inférieurs', derived 'Quadriceps'
[CATALOG] group mismatch EX250 'Step cardio': sheet label 'Membres inférieurs', derived 'Quadriceps'
```
</details>

Table counts after the first start: `ex 275 | eq 70 | obj 53 | exobj 561 | mg 14`.

## V2 — restart
```
MuscleGroupSeeder: created 0 muscle group(s).
CatalogSeeder equipment: created 0, existing 70, skipped 0, warnings 0.
CatalogSeeder objectives: created 0, existing 53, skipped 0, warnings 0.
CatalogSeeder primary groups: from muscles 248, from label 0, unresolved 0, mismatches 138, warnings 0.
CatalogSeeder exercises: created 0, adopted 0, already imported 248, skipped 2, objective links added 0, plural-only equipment matches 0, warnings 1.
[CATALOG] exercise EX135: source code used by 2 active rows ('Dead hang', 'Front squat'), all skipped
(138 group mismatch lines, identical to V1)
```
```
 ex  | eq | obj | exobj | mg 
-----+----+-----+-------+----
 275 | 70 |  53 |   561 | 14
(1 row)
```
Every `created` and `adopted` count is 0; table counts identical to after the first start.

## V3 — counts
```
 total | mentora | with_source | adopted | mentora_no_group | mentora_no_equipment | mentora_no_level | imported_no_group | imported_no_equipment | imported_no_level 
-------+---------+-------------+---------+------------------+----------------------+------------------+-------------------+-----------------------+-------------------
   275 |     275 |         248 |      18 |               27 |                  110 |               27 |                 0 |                    83 |                 0
(1 row)
```
`adopted` = source code set and created before 2026-09-29. The 27 Mentora rows without group/level are the 27 non-adopted historical exercises (V4); every imported row has a group and a level.
```
       grp       | count 
-----------------+-------
 Abdominaux      |    41
 Quadriceps      |    37
 Dos             |    32
 Épaules         |    30
 Pectoraux       |    30
 Bras            |    27
 (NULL)          |    27
 Fessiers        |    24
 Ischio-jambiers |    10
 Avant-bras      |     7
 Mollets         |     6
 Adducteurs      |     3
 Tibial          |     1
(13 rows)
```
Legacy columns of the 230 created rows:
```
 EXERCISE_MUSCLE_GROUP | count 
-----------------------+-------
 ABS                   |    37
 BACK                  |    30
 SHOULDERS             |    29
 CHEST                 |    26
 GLUTES                |    25
 QUADRICEPS            |    20
 CARDIO                |    18
 BICEPS                |    15
 TRICEPS               |    11
 HAMSTRINGS            |     8
 FOREARMS              |     7
 CALVES                |     4
(12 rows)

 EXERCISE_EQUIPMENT | count 
--------------------+-------
 DUMBBELL           |    50
 BODYWEIGHT         |    44
 OTHER              |    39
 CABLE              |    35
 MACHINE            |    26
 BARBELL            |    26
 ELASTIC            |    10
(7 rows)
```

## V4 — the 45 historical exercises
```
 historical | adopted 
------------+---------
         45 |      18
(1 row)

           not_adopted           
---------------------------------
 Burpees
 Course à pied (tapis)
 Crunch
 Curl biceps barre
 Curl biceps haltères alterné
 Curl marteau haltères
 Curl poignet barre
 Développé épaules barre
 Dips
 Écarté couché haltères
 Élévations frontales haltères
 Extension poignet haltère
 Extension triceps poulie haute
 Fentes avant
 Fentes bulgares
 Gainage planche
 Kettlebell swing
 Kickback à la poulie
 Mollets debout
 Oiseau (élévations arrière)
 Rotation russe
 Soulevé de terre jambes tendues
 Squat
 Suspension à la barre
 Thruster haltères
 Tirage horizontal poulie basse
 Traction / tirage vertical
(27 rows)

 code  |         adopted_name          | legacy_mg  | legacy_eq  
-------+-------------------------------+------------+------------
 EX001 | Développé couché barre        | CHEST      | BARBELL
 EX003 | Soulevé de terre              | BACK       | BARBELL
 EX004 | Rowing barre buste penché     | BACK       | BARBELL
 EX007 | Hip thrust barre              | GLUTES     | BARBELL
 EX010 | Élévations latérales haltères | SHOULDERS  | DUMBBELL
 EX013 | Rameur                        | CARDIO     | MACHINE
 EX016 | Développé couché haltères     | CHEST      | DUMBBELL
 EX018 | Développé incliné haltères    | CHEST      | DUMBBELL
 EX029 | Pompes                        | CHEST      | BODYWEIGHT
 EX052 | Rowing haltère unilatéral     | BACK       | DUMBBELL
 EX124 | Barre au front                | TRICEPS    | BARBELL
 EX140 | Presse à cuisses              | QUADRICEPS | MACHINE
 EX142 | Leg extension                 | QUADRICEPS | MACHINE
 EX154 | Good morning barre            | HAMSTRINGS | BARBELL
 EX155 | Leg curl allongé              | HAMSTRINGS | MACHINE
 EX164 | Mollets assis                 | CALVES     | MACHINE
 EX198 | Relevé de jambes suspendu     | ABS        | BODYWEIGHT
 EX235 | Corde à sauter                | CARDIO     | OTHER
(18 rows)
```

## V5 — group mismatches (138)
Group derived from EXERCICES_MUSCLES → MUSCLES differs from `groupe_musculaire_principal`. The derived group is the one stored.

Summary:

| Sheet label | Derived group | Exercises |
|---|---|---|
| Deltoïdes | Épaules | 26 |
| Dorsaux | Dos | 25 |
| Biceps | Bras | 14 |
| Membres inférieurs | Quadriceps | 13 |
| Triceps | Bras | 12 |
| Centre du corps | Abdominaux | 10 |
| Obliques | Abdominaux | 10 |
| Corps entier | Abdominaux | 7 |
| Trapèzes | Dos | 5 |
| Coiffe des rotateurs | Épaules | 4 |
| Corps entier | Quadriceps | 4 |
| Fléchisseurs avant-bras | Avant-bras | 2 |
| Lombaires | Dos | 2 |
| Brachio-radial | Bras | 1 |
| Chaîne postérieure | Fessiers | 1 |
| Extenseurs avant-bras | Avant-bras | 1 |
| Tibial antérieur | Tibial | 1 |

Full list:

| exercise_id | Name | Sheet label | Derived group |
|---|---|---|---|
| EX003 | Soulevé de terre | Chaîne postérieure | Fessiers |
| EX004 | Rowing barre buste penché | Dorsaux | Dos |
| EX005 | Tractions pronation | Dorsaux | Dos |
| EX008 | Curl haltères | Biceps | Bras |
| EX009 | Extension triceps à la poulie | Triceps | Bras |
| EX010 | Élévations latérales haltères | Deltoïdes | Épaules |
| EX011 | Planche | Centre du corps | Abdominaux |
| EX012 | Burpee | Corps entier | Quadriceps |
| EX013 | Rameur | Corps entier | Quadriceps |
| EX014 | Course à pied | Membres inférieurs | Quadriceps |
| EX015 | Vélo stationnaire | Membres inférieurs | Quadriceps |
| EX045 | Tirage vertical poulie haute | Dorsaux | Dos |
| EX046 | Tirage vertical prise neutre | Dorsaux | Dos |
| EX047 | Tirage vertical prise supination | Dorsaux | Dos |
| EX048 | Tirage vertical unilatéral | Dorsaux | Dos |
| EX049 | Tractions supination | Dorsaux | Dos |
| EX050 | Tractions prise neutre | Dorsaux | Dos |
| EX051 | Tractions assistées | Dorsaux | Dos |
| EX052 | Rowing haltère unilatéral | Dorsaux | Dos |
| EX053 | Rowing haltères buste penché | Dorsaux | Dos |
| EX054 | Rowing assis poulie basse | Dorsaux | Dos |
| EX055 | Rowing assis prise large | Dorsaux | Dos |
| EX056 | Rowing poulie basse unilatéral | Dorsaux | Dos |
| EX057 | Rowing poitrine appuyée haltères | Dorsaux | Dos |
| EX058 | Rowing T-bar | Dorsaux | Dos |
| EX059 | Rowing landmine unilatéral | Dorsaux | Dos |
| EX060 | Rowing machine assise | Dorsaux | Dos |
| EX061 | Rowing machine unilatéral | Dorsaux | Dos |
| EX062 | Rowing inversé | Dorsaux | Dos |
| EX063 | Rowing inversé aux anneaux | Dorsaux | Dos |
| EX064 | Tirage TRX | Dorsaux | Dos |
| EX065 | Pull-over poulie haute bras tendus | Dorsaux | Dos |
| EX066 | Pull-over haltère | Dorsaux | Dos |
| EX067 | Pull-over machine | Dorsaux | Dos |
| EX068 | Face pull | Trapèzes | Dos |
| EX069 | Reverse fly poulie | Trapèzes | Dos |
| EX070 | Reverse pec deck | Trapèzes | Dos |
| EX071 | Shrug haltères | Trapèzes | Dos |
| EX072 | Shrug barre | Trapèzes | Dos |
| EX073 | Extension lombaire au banc | Lombaires | Dos |
| EX074 | Superman au sol | Lombaires | Dos |
| EX075 | Développé militaire barre debout | Deltoïdes | Épaules |
| EX076 | Développé militaire barre assis | Deltoïdes | Épaules |
| EX077 | Développé épaules haltères assis | Deltoïdes | Épaules |
| EX078 | Développé épaules haltères debout | Deltoïdes | Épaules |
| EX079 | Développé épaules unilatéral haltère | Deltoïdes | Épaules |
| EX080 | Arnold press | Deltoïdes | Épaules |
| EX081 | Shoulder press machine | Deltoïdes | Épaules |
| EX082 | Shoulder press machine unilatérale | Deltoïdes | Épaules |
| EX083 | Smith machine développé épaules | Deltoïdes | Épaules |
| EX084 | Landmine press unilatéral | Deltoïdes | Épaules |
| EX085 | Landmine press à deux mains | Deltoïdes | Épaules |
| EX086 | Élévation latérale à la poulie unilatérale | Deltoïdes | Épaules |
| EX087 | Élévation latérale machine | Deltoïdes | Épaules |
| EX088 | Élévation latérale inclinée haltère | Deltoïdes | Épaules |
| EX089 | Élévation latérale couchée sur le côté | Deltoïdes | Épaules |
| EX090 | Élévation frontale haltères | Deltoïdes | Épaules |
| EX091 | Élévation frontale alternée haltères | Deltoïdes | Épaules |
| EX092 | Élévation frontale barre | Deltoïdes | Épaules |
| EX093 | Élévation frontale poulie | Deltoïdes | Épaules |
| EX094 | Y raise haltères | Deltoïdes | Épaules |
| EX095 | Oiseau haltères buste penché | Deltoïdes | Épaules |
| EX096 | Oiseau haltères sur banc incliné | Deltoïdes | Épaules |
| EX097 | Rowing coudes ouverts haltères | Deltoïdes | Épaules |
| EX098 | Rotation externe à la poulie | Coiffe des rotateurs | Épaules |
| EX099 | Rotation interne à la poulie | Coiffe des rotateurs | Épaules |
| EX100 | Rotation externe avec élastique | Coiffe des rotateurs | Épaules |
| EX101 | Rotation interne avec élastique | Coiffe des rotateurs | Épaules |
| EX102 | Scaption haltères | Deltoïdes | Épaules |
| EX103 | Cuban press haltères | Deltoïdes | Épaules |
| EX104 | Curl barre | Biceps | Bras |
| EX105 | Curl barre EZ | Biceps | Bras |
| EX106 | Curl incliné haltères | Biceps | Bras |
| EX107 | Curl marteau | Biceps | Bras |
| EX108 | Curl marteau alterné | Biceps | Bras |
| EX109 | Curl concentration | Biceps | Bras |
| EX110 | Curl pupitre barre EZ | Biceps | Bras |
| EX111 | Curl pupitre haltère | Biceps | Bras |
| EX112 | Curl poulie basse | Biceps | Bras |
| EX113 | Curl poulie unilatéral | Biceps | Bras |
| EX114 | Curl Bayesian | Biceps | Bras |
| EX115 | Curl spider | Biceps | Bras |
| EX116 | Curl inversé barre | Brachio-radial | Bras |
| EX117 | Curl Zottman | Biceps | Bras |
| EX118 | Dips triceps | Triceps | Bras |
| EX119 | Dips assistés | Triceps | Bras |
| EX120 | Extension triceps au-dessus de la tête haltère | Triceps | Bras |
| EX121 | Extension triceps au-dessus de la tête corde | Triceps | Bras |
| EX122 | Extension triceps poulie unilatérale | Triceps | Bras |
| EX123 | Extension triceps poulie prise inversée | Triceps | Bras |
| EX124 | Barre au front | Triceps | Bras |
| EX125 | Extension triceps haltères couchée | Triceps | Bras |
| EX126 | Kickback triceps haltère | Triceps | Bras |
| EX127 | Kickback triceps poulie | Triceps | Bras |
| EX128 | Développé couché prise serrée triceps | Triceps | Bras |
| EX129 | Extension poignets barre | Fléchisseurs avant-bras | Avant-bras |
| EX130 | Extension poignets inversée | Extenseurs avant-bras | Avant-bras |
| EX131 | Curl poignets haltères | Fléchisseurs avant-bras | Avant-bras |
| EX167 | Tibial raise | Tibial antérieur | Tibial |
| EX203 | Bird dog | Centre du corps | Abdominaux |
| EX204 | Planche latérale | Obliques | Abdominaux |
| EX205 | Planche latérale sur genoux | Obliques | Abdominaux |
| EX206 | Planche latérale avec abduction | Obliques | Abdominaux |
| EX207 | Pallof press | Obliques | Abdominaux |
| EX208 | Pallof press avec élastique | Obliques | Abdominaux |
| EX209 | Wood chop poulie haute vers basse | Obliques | Abdominaux |
| EX210 | Wood chop poulie basse vers haute | Obliques | Abdominaux |
| EX211 | Russian twist | Obliques | Abdominaux |
| EX212 | Mountain climber | Centre du corps | Abdominaux |
| EX213 | Planche haute | Centre du corps | Abdominaux |
| EX214 | Planche avec toucher d'épaule | Centre du corps | Abdominaux |
| EX215 | Planche avec déplacement avant-arrière | Centre du corps | Abdominaux |
| EX216 | Ab wheel rollout | Centre du corps | Abdominaux |
| EX217 | Stir the pot | Centre du corps | Abdominaux |
| EX223 | Heel touches | Obliques | Abdominaux |
| EX224 | Suitcase hold | Obliques | Abdominaux |
| EX225 | Front rack carry | Centre du corps | Abdominaux |
| EX226 | Overhead carry | Centre du corps | Abdominaux |
| EX227 | Air bike | Corps entier | Quadriceps |
| EX228 | SkiErg | Corps entier | Quadriceps |
| EX229 | Tapis de course | Membres inférieurs | Quadriceps |
| EX230 | Marche inclinée | Membres inférieurs | Quadriceps |
| EX231 | Sprint sur tapis | Membres inférieurs | Quadriceps |
| EX232 | Vélo spinning | Membres inférieurs | Quadriceps |
| EX233 | Elliptique | Corps entier | Abdominaux |
| EX234 | Stair climber | Membres inférieurs | Quadriceps |
| EX237 | Battle rope vagues alternées | Corps entier | Abdominaux |
| EX238 | Battle rope vagues simultanées | Corps entier | Abdominaux |
| EX239 | Sled push | Membres inférieurs | Quadriceps |
| EX240 | Sled pull | Corps entier | Abdominaux |
| EX243 | Sandbag carry | Corps entier | Abdominaux |
| EX244 | Bear crawl | Corps entier | Abdominaux |
| EX245 | Crab walk | Corps entier | Abdominaux |
| EX246 | Shuttle run | Membres inférieurs | Quadriceps |
| EX247 | Sprint extérieur | Membres inférieurs | Quadriceps |
| EX248 | Marche rapide | Membres inférieurs | Quadriceps |
| EX249 | Montée d'escaliers | Membres inférieurs | Quadriceps |
| EX250 | Step cardio | Membres inférieurs | Quadriceps |

Note for the product owner: most rows are vocabulary differences (Dorsaux/Trapèzes/Lombaires → Dos, Deltoïdes → Épaules, Biceps/Triceps → Bras, Obliques → Abdominaux). The cardio / whole-body rows (EX012–EX015, EX227–EX250) derive Quadriceps or Abdominaux from their lowest-importance principal muscle; their legacy column is still CARDIO when `categorie` contains "Cardio".

## V6 — equipment and levels
Plural-only matches (matched only thanks to the trailing-`s` rule):

```
[CATALOG] plural-only equipment match 'Haltère' -> 'Haltères' x12: EX052, EX066, EX079, EX088, EX089, EX109, EX111, EX120, EX126, EX132, EX147, EX166
```

Still unmatched (FK left NULL):

| Value | Count | Exercises (slot) |
|---|---|---|
| Poids du corps | 36 | EX006 (primary), EX011 (primary), EX012 (primary), EX074 (primary), EX145 (primary), EX146 (primary), EX147 (primary), EX166 (primary), EX167 (primary), EX168 (primary), EX170 (primary), EX171 (primary), EX173 (primary), EX175 (primary), EX176 (primary), EX179 (primary), EX180 (primary), EX184 (primary), EX193 (primary), EX196 (primary), EX200 (primary), EX201 (primary), EX203 (primary), EX204 (primary), EX205 (primary), EX206 (primary), EX212 (primary), EX213 (primary), EX214 (primary), EX218 (primary), EX219 (primary), EX221 (primary), EX222 (primary), EX223 (primary), EX244 (primary), EX245 (primary) |
| Banc | 12 | EX001 (secondary), EX007 (secondary), EX052 (secondary), EX066 (secondary), EX076 (secondary), EX124 (secondary), EX125 (secondary), EX126 (secondary), EX128 (secondary), EX144 (secondary), EX175 (secondary), EX192 (secondary) |
| Poignée simple | 12 | EX044 (secondary), EX048 (secondary), EX056 (secondary), EX086 (secondary), EX093 (secondary), EX098 (secondary), EX099 (secondary), EX113 (secondary), EX114 (secondary), EX122 (secondary), EX127 (secondary), EX207 (secondary) |
| Barre | 6 | EX092 (primary), EX112 (secondary), EX116 (primary), EX123 (secondary), EX129 (primary), EX130 (primary) |
| Sans matériel | 5 | EX029 (primary), EX030 (primary), EX033 (primary), EX034 (primary), EX037 (primary) |
| Corde | 4 | EX068 (secondary), EX121 (secondary), EX189 (secondary), EX195 (secondary) |
| Support fixe | 4 | EX088 (secondary), EX150 (primary), EX178 (secondary), EX208 (secondary) |
| Aucun | 3 | EX014 (primary), EX247 (primary), EX248 (primary) |
| Haltère ou kettlebell | 3 | EX134 (primary), EX153 (primary), EX224 (primary) |
| Haltères ou kettlebells | 3 | EX133 (primary), EX226 (primary), EX242 (primary) |
| Landmine | 3 | EX059 (secondary), EX084 (secondary), EX085 (secondary) |
| Sangle cheville | 3 | EX162 (secondary), EX177 (secondary), EX182 (secondary) |
| Banc lombaire | 2 | EX073 (primary), EX191 (primary) |
| Banc pupitre | 2 | EX110 (secondary), EX111 (secondary) |
| Barre longue | 2 | EX045 (secondary), EX047 (secondary) |
| Poignée | 2 | EX209 (secondary), EX210 (secondary) |
| Poignée neutre | 2 | EX046 (secondary), EX054 (secondary) |
| Rack | 2 | EX002 (secondary), EX154 (secondary) |
| Anneaux gymnastique | 1 | EX063 (primary) |
| Banc ou machine reverse hyper | 1 | EX190 (primary) |
| Banc ou tapis | 1 | EX089 (secondary) |
| Barre fixe | 1 | EX062 (primary) |
| Barre large | 1 | EX055 (secondary) |
| Barre olympique ou haltère | 1 | EX172 (primary) |
| Barre ou corde | 1 | EX065 (secondary) |
| Barres parallèles ou parallettes | 1 | EX220 (primary) |
| Chaise romaine | 1 | EX199 (primary) |
| Corde / barre | 1 | EX009 (secondary) |
| Corde ou sangles | 1 | EX240 (secondary) |
| Disque ou haltère | 1 | EX194 (primary) |
| Escaliers | 1 | EX249 (primary) |
| Hip thrust machine | 1 | EX174 (primary) |
| Kettlebell ou haltère | 1 | EX136 (primary) |
| Kettlebells ou haltères | 1 | EX225 (primary) |
| Leg curl | 1 | EX157 (primary) |
| Machine mollets | 1 | EX163 (primary) |
| Machine mollets assis | 1 | EX164 (primary) |
| Machine pullover | 1 | EX067 (primary) |
| Machine à dips assistés | 1 | EX119 (primary) |
| Machine à tractions assistées | 1 | EX051 (primary) |
| Machine élévations latérales | 1 | EX087 (primary) |
| Mur | 1 | EX167 (secondary) |
| Poids du corps ou medicine ball | 1 | EX211 (primary) |
| Poignées parallèles | 1 | EX050 (secondary) |
| Roue abdominale | 1 | EX216 (primary) |
| Sangles | 1 | EX241 (secondary) |
| Sliders | 1 | EX160 (primary) |
| Sliders ou TRX | 1 | EX215 (primary) |
| Support de chevilles | 1 | EX158 (primary) |
| T-bar | 1 | EX058 (primary) |
| Vélo | 1 | EX015 (primary) |
| Vélo spinning | 1 | EX232 (primary) |

52 distinct values, 140 occurrences.

Unknown levels among processed rows: none.
The only non-standard `niveau_minimum` in the file (`Gainage ; Contrôle moteur ; Renforcement`) is on EX202 Dead bug, which is not `actif = Oui`.

## V7 — equipment legacy mapping
Keyword search order per row: name, then subcategory, then category (see deviations). For review:
```
  code  |             name              |         cat          |          sub           |   legacy   
--------+-------------------------------+----------------------+------------------------+------------
 MAT001 | Haltères                      | Charge libre         | Haltères               | DUMBBELL
 MAT002 | Barre olympique               | Charge libre         | Barre                  | BARBELL
 MAT003 | Disques olympiques            | Charge libre         | Disques                | BARBELL
 MAT004 | Barre EZ                      | Charge libre         | Barre                  | BARBELL
 MAT005 | Kettlebell                    | Charge libre         | Kettlebell             | KETTLEBELL
 MAT006 | Banc plat                     | Mobilier             | Banc                   | OTHER
 MAT007 | Banc réglable                 | Mobilier             | Banc                   | OTHER
 MAT008 | Rack à squat                  | Structure            | Rack                   | OTHER
 MAT009 | Cage à squat                  | Structure            | Cage                   | OTHER
 MAT010 | Smith machine                 | Machine guidée       | Charge guidée          | MACHINE
 MAT011 | Poulie haute                  | Machine à câble      | Poulie                 | CABLE
 MAT012 | Poulie basse                  | Machine à câble      | Poulie                 | CABLE
 MAT013 | Poulie réglable               | Machine à câble      | Poulie                 | CABLE
 MAT014 | Vis-à-vis                     | Machine à câble      | Double poulie          | CABLE
 MAT015 | Presse à cuisses              | Machine guidée       | Jambes                 | MACHINE
 MAT016 | Hack squat                    | Machine guidée       | Jambes                 | MACHINE
 MAT017 | Leg extension                 | Machine guidée       | Jambes                 | MACHINE
 MAT018 | Leg curl assis                | Machine guidée       | Jambes                 | MACHINE
 MAT019 | Leg curl allongé              | Machine guidée       | Jambes                 | MACHINE
 MAT020 | Machine adducteurs            | Machine guidée       | Hanche                 | MACHINE
 MAT021 | Machine abducteurs            | Machine guidée       | Hanche                 | MACHINE
 MAT022 | Pec deck                      | Machine guidée       | Pectoraux              | MACHINE
 MAT023 | Reverse pec deck              | Machine guidée       | Épaules / dos          | MACHINE
 MAT024 | Chest press                   | Machine guidée       | Pectoraux              | MACHINE
 MAT025 | Shoulder press                | Machine guidée       | Épaules                | MACHINE
 MAT026 | Lat pulldown                  | Machine guidée       | Dos                    | MACHINE
 MAT027 | Rowing assis                  | Machine guidée       | Dos                    | MACHINE
 MAT028 | Barre de traction             | Structure            | Barre fixe             | BARBELL
 MAT029 | Barres parallèles             | Structure            | Dips                   | BARBELL
 MAT030 | Élastique                     | Résistance élastique | Bande                  | ELASTIC
 MAT031 | Mini-band                     | Résistance élastique | Bande courte           | ELASTIC
 MAT032 | TRX                           | Suspension           | Sangles                | OTHER
 MAT033 | Anneaux de gymnastique        | Suspension           | Anneaux                | OTHER
 MAT034 | Medicine ball                 | Charge fonctionnelle | Ballon lesté           | OTHER
 MAT035 | Slam ball                     | Charge fonctionnelle | Ballon lesté           | OTHER
 MAT036 | Wall ball                     | Charge fonctionnelle | Ballon lesté           | OTHER
 MAT037 | Sandbag                       | Charge fonctionnelle | Sac lesté              | OTHER
 MAT038 | Gilet lesté                   | Charge portée        | Gilet                  | OTHER
 MAT039 | Box pliométrique              | Pliométrie           | Box                    | OTHER
 MAT040 | Step                          | Cardio / fonctionnel | Plateforme             | OTHER
 MAT041 | Corde à sauter                | Cardio               | Corde                  | OTHER
 MAT042 | Battle rope                   | Conditionnement      | Corde lourde           | OTHER
 MAT043 | Sled                          | Conditionnement      | Traîneau               | OTHER
 MAT044 | Prowler                       | Conditionnement      | Traîneau               | OTHER
 MAT045 | Corde à grimper               | Structure            | Corde                  | OTHER
 MAT046 | Tapis de sol                  | Accessoire           | Tapis                  | OTHER
 MAT047 | Foam roller                   | Récupération         | Auto-massage           | OTHER
 MAT048 | Ballon suisse                 | Instabilité          | Ballon                 | OTHER
 MAT049 | BOSU                          | Instabilité          | Demi-ballon            | OTHER
 MAT050 | Balance d'équilibre           | Évaluation           | Proprioception         | OTHER
 MAT051 | Rameur                        | Cardio               | Ergomètre              | OTHER
 MAT052 | Vélo stationnaire             | Cardio               | Ergomètre              | OTHER
 MAT053 | Air bike                      | Cardio               | Ergomètre              | OTHER
 MAT054 | Tapis de course               | Cardio               | Ergomètre              | OTHER
 MAT055 | SkiErg                        | Cardio               | Ergomètre              | OTHER
 MAT056 | Elliptique                    | Cardio               | Ergomètre              | OTHER
 MAT057 | Escalier / Stair Climber      | Cardio               | Ergomètre              | OTHER
 MAT058 | Balance                       | Évaluation           | Anthropométrie         | OTHER
 MAT059 | Balance impédancemètre        | Évaluation           | Composition corporelle | OTHER
 MAT060 | Pince à plis cutanés          | Évaluation           | Anthropométrie         | OTHER
 MAT061 | Mètre ruban                   | Évaluation           | Anthropométrie         | OTHER
 MAT062 | Toise                         | Évaluation           | Anthropométrie         | OTHER
 MAT063 | Cardiofréquencemètre          | Évaluation           | Cardio                 | OTHER
 MAT064 | Montre cardio / GPS           | Évaluation           | Cardio / GPS           | OTHER
 MAT065 | Chronomètre                   | Évaluation           | Temps                  | OTHER
 MAT066 | Plots                         | Accessoire           | Balisage               | OTHER
 MAT067 | Mètre de distance / décamètre | Évaluation           | Distance               | OTHER
 MAT068 | Dynamomètre                   | Évaluation           | Force                  | OTHER
 MAT069 | Plateforme de saut            | Évaluation           | Puissance              | OTHER
 MAT070 | Smartphone / caméra           | Évaluation           | Vidéo                  | OTHER
(70 rows)
```

## V8 — full rows
EX001 and EX010 are adopted historical rows (original description, instructions and legacy columns kept; NULL columns filled). EX104 is a created row.
```
Expanded display is on.
-[ RECORD 1 ]--------------------+--------------------------------------------------------------------------------------------------------------------------------------------------
EXERCISE_ID                      | 1194fd80-be00-4630-907e-80609ef0f331
EXERCISE_COACH_ID                | 
EXERCISE_NAME                    | Développé couché barre
EXERCISE_DESCRIPTION             | Exercice polyarticulaire de référence pour les pectoraux.
EXERCISE_INSTRUCTIONS            | Allongé sur un banc, saisir la barre à largeur épaules, descendre jusqu'à effleurer la poitrine puis pousser jusqu'à extension complète des bras.
EXERCISE_VIDEO_URL               | 
EXERCISE_IMAGE_URL               | 
EXERCISE_MUSCLE_GROUP            | CHEST
EXERCISE_EQUIPMENT               | BARBELL
EXERCISE_IS_POLYARTICULAR        | t
EXERCISE_IS_ACTIVE               | t
EXERCISE_CREATED_DATE            | 2026-08-11 11:53:42.833197+00
EXERCISE_UPDATED_DATE            | 2026-09-29 17:46:41.616204+00
EXERCISE_BREATHING               | Inspirer à la descente ; expirer lors de la poussée
EXERCISE_CATEGORY                | Musculation
EXERCISE_COACH_TIP               | 
EXERCISE_COMMON_MISTAKES         | ["Décoller les épaules", "rebondir sur la poitrine", "poignets cassés", "pieds instables"]
EXERCISE_GRIP_TYPE               | Pronée
EXERCISE_LATERALITY              | Bilatéral
EXERCISE_MIN_LEVEL               | INTERMEDIATE
EXERCISE_MOVEMENT_PATTERN        | Poussée horizontale
EXERCISE_NAME_EN                 | Barbell Bench Press
EXERCISE_PRECAUTIONS             | Adapter l'amplitude en cas d'inconfort à l'épaule
EXERCISE_PRIMARY_EQUIPMENT_ID    | 04dbd52c-185c-4fab-928b-19d904473e25
EXERCISE_PRIMARY_MUSCLE_GROUP_ID | d6660410-7110-4ac5-8f08-c90ac7f1288d
EXERCISE_SEARCH_TAGS             | développé couché ; bench press ; pecs ; barre
EXERCISE_SECONDARY_EQUIPMENT_ID  | 
EXERCISE_SECONDARY_MUSCLES       | ["Triceps", "deltoïde antérieur"]
EXERCISE_SOURCE_CODE             | EX001
EXERCISE_STARTING_POSITION       | Couché sur banc
EXERCISE_TYPE                    | Polyarticulaire
primary_group                    | Pectoraux
primary_eq                       | Barre olympique
secondary_eq                     | 
objectives                       | Force ; Hypertrophie
-[ RECORD 2 ]--------------------+--------------------------------------------------------------------------------------------------------------------------------------------------
EXERCISE_ID                      | 62fe1f40-4d66-4d83-824b-e927f2c015eb
EXERCISE_COACH_ID                | 
EXERCISE_NAME                    | Élévations latérales haltères
EXERCISE_DESCRIPTION             | Mouvement d'isolation du deltoïde moyen.
EXERCISE_INSTRUCTIONS            | Debout, un haltère dans chaque main, lever les bras sur les côtés jusqu'à hauteur d'épaule puis redescendre.
EXERCISE_VIDEO_URL               | 
EXERCISE_IMAGE_URL               | 
EXERCISE_MUSCLE_GROUP            | SHOULDERS
EXERCISE_EQUIPMENT               | DUMBBELL
EXERCISE_IS_POLYARTICULAR        | f
EXERCISE_IS_ACTIVE               | t
EXERCISE_CREATED_DATE            | 2026-08-11 11:53:42.833197+00
EXERCISE_UPDATED_DATE            | 2026-09-29 17:46:41.616204+00
EXERCISE_BREATHING               | Expirer à la montée ; inspirer à la descente
EXERCISE_CATEGORY                | Musculation
EXERCISE_COACH_TIP               | 
EXERCISE_COMMON_MISTAKES         | ["Hausser les épaules", "utiliser trop d'élan", "charge excessive"]
EXERCISE_GRIP_TYPE               | Neutre
EXERCISE_LATERALITY              | Bilatéral
EXERCISE_MIN_LEVEL               | BEGINNER
EXERCISE_MOVEMENT_PATTERN        | Abduction épaule
EXERCISE_NAME_EN                 | Dumbbell Lateral Raise
EXERCISE_PRECAUTIONS             | Réduire l'amplitude en cas d'inconfort à l'épaule
EXERCISE_PRIMARY_EQUIPMENT_ID    | 63b68ad4-a46f-44c0-89ba-23c56be801e5
EXERCISE_PRIMARY_MUSCLE_GROUP_ID | fc704ef2-1d0d-414e-8a67-5b507721584e
EXERCISE_SEARCH_TAGS             | élévations latérales ; épaules ; deltoïdes ; lateral raise
EXERCISE_SECONDARY_EQUIPMENT_ID  | 
EXERCISE_SECONDARY_MUSCLES       | ["Trapèzes"]
EXERCISE_SOURCE_CODE             | EX010
EXERCISE_STARTING_POSITION       | Debout
EXERCISE_TYPE                    | Monoarticulaire
primary_group                    | Épaules
primary_eq                       | Haltères
secondary_eq                     | 
objectives                       | Hypertrophie
-[ RECORD 3 ]--------------------+--------------------------------------------------------------------------------------------------------------------------------------------------
EXERCISE_ID                      | c2ff6721-89b6-4711-973f-937c17f4a6e1
EXERCISE_COACH_ID                | 
EXERCISE_NAME                    | Curl barre
EXERCISE_DESCRIPTION             | Fléchir les coudes sans avancer les épaules
EXERCISE_INSTRUCTIONS            | Garder les bras près du corps, monter la barre jusqu'à contraction des biceps puis redescendre sous contrôle
EXERCISE_VIDEO_URL               | 
EXERCISE_IMAGE_URL               | 
EXERCISE_MUSCLE_GROUP            | BICEPS
EXERCISE_EQUIPMENT               | BARBELL
EXERCISE_IS_POLYARTICULAR        | f
EXERCISE_IS_ACTIVE               | t
EXERCISE_CREATED_DATE            | 2026-09-29 17:46:41.616204+00
EXERCISE_UPDATED_DATE            | 2026-09-29 17:46:41.616204+00
EXERCISE_BREATHING               | Expirer à la montée ; inspirer à la descente
EXERCISE_CATEGORY                | Musculation
EXERCISE_COACH_TIP               | 
EXERCISE_COMMON_MISTAKES         | ["Balancer le buste", "avancer les coudes", "casser les poignets"]
EXERCISE_GRIP_TYPE               | Supination
EXERCISE_LATERALITY              | Bilatéral
EXERCISE_MIN_LEVEL               | BEGINNER
EXERCISE_MOVEMENT_PATTERN        | Flexion de coude
EXERCISE_NAME_EN                 | Barbell Curl
EXERCISE_PRECAUTIONS             | Adapter la charge pour éviter l'élan
EXERCISE_PRIMARY_EQUIPMENT_ID    | 04dbd52c-185c-4fab-928b-19d904473e25
EXERCISE_PRIMARY_MUSCLE_GROUP_ID | 08d8cdd3-09e7-45ff-8515-7850c6f036ed
EXERCISE_SEARCH_TAGS             | curl barre ; barbell curl ; biceps
EXERCISE_SECONDARY_EQUIPMENT_ID  | 
EXERCISE_SECONDARY_MUSCLES       | ["Avant-bras"]
EXERCISE_SOURCE_CODE             | EX104
EXERCISE_STARTING_POSITION       | Debout
EXERCISE_TYPE                    | Monoarticulaire
primary_group                    | Bras
primary_eq                       | Barre olympique
secondary_eq                     | 
objectives                       | Force ; Hypertrophie
```

## V9 — integrity
```
          chk          | count 
-----------------------+-------
 dup source code       |     0
 dup (coach,name)      |     0
 orphan link exercise  |     0
 orphan link objective |     0
 dup equipment source  |     0
 objectives unused     |     1
(6 rows)
```
The unused objective is `Suspension`, only referenced by EX135 Dead hang (skipped).

## V10 — public API
```
otp/request coach@mentora.fr -> 200
otp/request member@mentora.fr -> 200
GET /api/v1/coach/exercises -> HTTP 200; envelope: success,data,error,statusCode; items: 275
  keys: exerciseId,coachId,name,description,instructions,videoUrl,imageUrl,muscleGroup,equipment,isPolyarticular,isActive,createdDate,updatedDate | all items same 13 keys: True
GET /api/v1/member/exercises -> HTTP 200; envelope: success,data,error,statusCode; items: 275
  keys: exerciseId,coachId,name,description,instructions,videoUrl,imageUrl,muscleGroup,equipment,isPolyarticular,isActive,createdDate,updatedDate | all items same 13 keys: True
Test-Path token file: False
```
Real OTP flow for the existing `coach@mentora.fr` / `member@mentora.fr`. Side effect: AUTH_OTPS 41→43, AUTH_REFRESH_TOKENS 40→42 (technical auth rows); USERS stays 2. Total went from 45 to 275.

## V11 — build
`dotnet build --no-incremental`: 19 warnings (same as the baseline), 0 errors.

## Deviations
- Duplicate `exercise_id` (EX135): all rows skipped (PO decision 1). Duplicate normalized names are handled the same way (all rows skipped); the sheet currently has none.
- Legacy equipment fallback on the raw text with `aucun` (PO decision 2); plural-tolerant equipment FK match (PO decision 3).
- Equipment keyword search order is name → subcategory → category, so `Vis-à-vis | Machine à câble | Double poulie` maps to CABLE instead of MACHINE. Only this row is affected.
- Unmatched equipment and unknown levels are logged as one aggregated warning line per distinct value (with the exercise ids), not one line per row.
- `[CATALOG] group mismatch` lines are logged on every start (step 3.3 runs each time) and counted separately from warnings.
- Objective links are only added to exercises created or adopted in the run; an already-imported exercise is not touched at all, links included.
- Extra safety guards, never triggered on this sheet: column-length checks that skip instead of truncating; skip when a new name collides with another imported exercise or with several adoptable rows; skip when an equipment name is already used by another EQUIPMENT row.
