# Territoires en arcs, biomes et relief de fond — plan de construction

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** remplir toute la carte avec 4 territoires en quarts de cercle de 1 500 m + un centre neutre, chacun avec son biome, un vrai relief de fond (collines, dunes, montagnes, rivières, cascades), des ponts, de la végétation et des arches, et des villages agrandis à 700 m — sans casser ce qui marche (apparition, portes, domaines, souterrains, bord).

**Architecture :** tout part de `tools/carte.py` (zones, biomes) et `tools/relief.py` (hauteur analytique `height_at`, eau, ponts). `tools/plans-villages-v3.py` (plans) et `tools/plan-vers-blocage.py` (script `run_csharp`) posent le blocage sur ce relief ; `tools/terrain-import.cs.txt` écrit hauteurs + carte des sols dans le Terrain. Les tests Python vérifient la géométrie hors éditeur ; les scripts `tools/tests/*.cs` la vérifient en jeu.

**Tech Stack :** Python 3 (sans dépendance), s&box (C#, `sbox-cli` : `run_csharp`, `play_start`, `screenshot`), composant `Terrain`.

**Spec :** `docs/superpowers/specs/2026-11-10-territoires-biomes-relief-design.md` (à lire en entier par chaque exécutant ; le projet précédent `2026-11-10-carte-coins-relief-souterrains-design.md` reste valable sauf ce que la nouvelle spec change).

**Branche :** créer `territoires` depuis `carte-coins` (HEAD `20411fc`).

## Global Constraints

- Unités : 40 u = 1 m. Carte 112 000 u (2 800 m), zone jouable `|x|,|y| ≤ INNER = 50 000 u`, bande de bord infranchissable au-delà (inchangée).
- Coins : Konoha (-50 000, 50 000), Suna (50 000, 50 000), Kumo (50 000, -50 000), 4e coin `libre` (-50 000, -50 000).
- Pays = quart de cercle de **1 500 m** centré sur son coin, coupé par x = 0 et y = 0. Centre commun = le reste de la zone jouable (losange aux côtés arrondis), neutre.
- Villages : rayon **700 m** (×1,4) ; les bâtiments **gardent leur taille**, seules les positions s'étirent.
- Biomes : Konoha = forêt, Suna = désert, Kumo = roche, 4e coin et centre = neutre. Fondu de **80 m** de large centré sur chaque frontière (damier accepté si le fondu doux est impossible).
- Hors villages et bord : **tout est marchable**, pente ≤ 35° (sauf parois de ravin / berges, qui ont toujours une sortie marchable à chaque bout). Pas de ravin aux frontières : on passe à pied partout.
- Routes porte → centre : aplanies (≤ 10° en long, plat en travers sur 20 m), un pont sur chaque eau / ravin qu'elles croisent, un poste-frontière en bord de route à chaque frontière traversée.
- Végétation : dense à Konoha (pays + parcs du village), légère à Kumo, aucune à Suna (sauf oasis futures) ; 3 à 6 arches à Suna ; 4 à 8 cascades (rivières / ravins de Konoha, falaises de Kumo). Blocage uniquement (formes simples), posé avec `height_at`.
- Sol gris toléré jusqu'à la tâche 7 ; à la fin les 4 biomes se voient en jeu.
- ≥ 55 i/s minimum sur 2 s dans chaque village **et** au cœur de la forêt de Konoha (aujourd'hui ≥ 210 dans les villages).
- Règles projet (`CLAUDE.md`) : français dans les commentaires et docs ; toute API s&box non listée dans `docs/sbox-reference.md` est vérifiée dans le code source de s&box puis ajoutée ; chaque morceau de code passe par le `relecteur` ; `docs/PROGRESS.md` à jour + commit à la fin de chaque tâche.
- Le relief (heightfield) ne bloque rien par en dessous : tout souterrain déplacé garde son plafond / toit de puits.

## Review Focus

1. **Village agrandi qui déborde ou se chevauche** : à 700 m, un bâtiment, une sortie cachée X* ou un puits peut sortir du quart de cercle, tomber dans la bande de bord, ou couper une rampe → tests de `test_plans.py` sur l'étendue (tâche 3).
2. **Joueur coincé ou qui tombe** : point d'apparition sur une colline ou dans un arbre, fond de ravin sans sortie, cascade avec collision → test de chemin + tests d'apparition (tâches 2, 4, 6).
3. **Route coupée** : une rivière / un ravin / une dune traverse une route sans pont, ou la route monte à > 10° → test des routes (tâche 2).
4. **Contenu replacé qui flotte ou s'enfonce** (forêt, clairières, temple, marché, arènes posés à l'ancienne hauteur 0) ou qui finit hors de sa zone (contenu du centre dans un pays) → test en jeu « posé au sol » (tâche 6).
5. **Script de blocage relancé deux fois** : le déplacement du contenu existant (`plan-vers-blocage.py`, passe (b)) s'applique à des objets déjà déplacés → il doit être idempotent (même résultat à la 2e exécution), testé en tâche 6.

---

### Task 1 : Territoires et biomes (`carte.py`)

**Files :**
- Modify : `tools/carte.py`
- Create : `tools/test_carte.py`

**Interfaces :**
- Produces :
  - constantes `PAYS_R = 1500`, `VILLAGE_R = 700`, `ECHELLE_VILLAGE = 1.4`, `FONDU_M = 80` (mètres) ; `BIOME = {"konoha": "foret", "suna": "desert", "kumo": "roche", "libre": "neutre", "centre": "neutre"}` ;
  - `territoire(x_u: float, y_u: float) -> str` dans `{"konoha", "suna", "kumo", "libre", "centre", "bord"}` (bord = hors `INNER`) ; sur la ligne x = 0 / y = 0 exactement : x ≥ 0 → côté est, y ≥ 0 → côté nord ;
  - `biome_poids(x_u, y_u) -> dict[str, float]` clés `foret, desert, roche, neutre`, somme 1, fondu linéaire sur `FONDU_M` centré sur la frontière la plus proche ;
  - `dist_frontiere_m(x_u, y_u) -> float` (distance en m à la frontière de territoire la plus proche, utile au relief et au décor).
- `zone_of` existante : la réécrire sur `territoire` (garder ses valeurs `village-<v>`, `pays-<v>`, `centre`, `bord-<v>` ; plus de `neutre` hors centre) et adapter ses appelants (`grep -n zone_of tools`).

- [ ] **Step 1 : tests qui échouent** dans `tools/test_carte.py` (même style que `test_relief.py` : fonctions + `assert`, exécutable par `python tools/test_carte.py`) :
  - `territoire(-30000, 30000) == "konoha"`, `territoire(30000, 30000) == "suna"`, `territoire(30000, -30000) == "kumo"`, `territoire(-30000, -30000) == "libre"` ;
  - `territoire(0, 0) == "centre"`, `territoire(-8000, 8000) == "konoha"` (1 485 m du coin), `territoire(-6000, 6000) == "centre"` (1 556 m) ;
  - `territoire(-100, 20000) == "konoha"`, `territoire(100, 20000) == "suna"`, `territoire(-100, 15000) == "centre"` ;
  - `territoire(-52000, 0) == "bord"` ;
  - `biome_poids(-30000, 30000)["foret"] == 1.0` ; `biome_poids(-2000, 30000)["foret"] == 1.0` (50 m de la frontière) ; à `(0, 30000)` : `foret` et `desert` à 0,5 ± 0,05 ; pour 2 000 points aléatoires (graine fixe) : somme = 1 ± 1e-9 et chaque poids dans [0, 1] ;
  - continuité : sur une ligne qui traverse chaque frontière, aucun poids ne saute de plus de 0,1 entre deux points espacés de 2 m.
- [ ] **Step 2 :** `python tools/test_carte.py` → échec (fonctions absentes).
- [ ] **Step 3 :** implémenter dans `tools/carte.py`.
- [ ] **Step 4 :** `python tools/test_carte.py`, `python tools/test_relief.py`, `python tools/test_plans.py` → tous OK (les deux derniers peuvent déjà échouer à cause de `VILLAGE_R` / `PAYS_R` changés : dans ce cas, garder temporairement les anciennes valeurs pour les appelants qui en dépendent sous un autre nom, ex. `ANCIEN_PAYS_R`, jusqu'aux tâches 2-3, et le noter dans le rapport).
- [ ] **Step 5 :** commit `Carte : territoires en quarts de cercle de 1 500 m, biomes et fondu`.

### Task 2 : Relief de fond, eau et ponts (`relief.py`)

**Files :**
- Modify : `tools/relief.py`, `tools/test_relief.py`

**Interfaces :**
- Consumes : `territoire`, `biome_poids`, `dist_frontiere_m`, `BIOME`, `VILLAGE_R`, `PAYS_R` (tâche 1).
- Produces (lus par les tâches 4 à 6) :
  - `height_at(x_u, y_u)` inchangée de signature, maintenant avec le relief de fond ;
  - `ROUTES : list[dict]` `{v, pts: [(x_u, y_u), …], larg_u}` une par village (porte → bord du centre) ; le relief est aplani dessus ;
  - `EAUX : list[dict]` `{nom, pts: [(x_u, y_u, z_eau), …], larg_u}` rivières (z de l'eau décroissant vers l'aval) ;
  - `CASCADES : list[dict]` `{x, y, z_haut, z_bas, larg_u, yaw}` (4 à 8) ;
  - `PONTS : list[dict]` `{x, y, yaw, long_u, larg_u, z}` (route × eau / ravin, ponts secondaires, ponts entre pics de Kumo) ;
  - `PICS_KUMO : list[(x_u, y_u, r_u, z_sommet)]`.
- Le relief dans le village (niveaux, rampes, `TIER_Z`, `RAMPS`) ne change pas ici ; l'agrandissement est la tâche 3. Ici : `RMAX` et le raccord village ↔ fond suivent `VILLAGE_R` (le fond s'efface sur 60 m autour du rempart).

Choix déjà faits (spec) : collines arrondies + vallons à Konoha, dunes allongées + quelques plateaux à Suna, grosses bosses rocheuses montant vers le coin à Kumo, ondulations faibles au 4e coin, centre presque plat. Rivières / ravins avec cascades **dans le pays de Konoha** ; cascades aussi sur des marches raides de Kumo. Technique de bruit libre (bruit de valeur / somme de sinus, sans dépendance, déterministe).

- [ ] **Step 1 : tests qui échouent** dans `tools/test_relief.py` (ajouter aux tests existants, garder ceux qui restent valables) :
  - `pentes_marchables` : grille de 10 m sur la zone jouable hors villages (distance au coin > `VILLAGE_R` + 20 m), hors berges d'eau (≤ `larg_u`/2 + 30 m d'une `EAUX`) et hors bande de bord : pente entre voisins ≤ 35° partout ;
  - `routes` : le long de chaque `ROUTES`, tous les 5 m : pente en long ≤ 10°, écart de hauteur en travers ≤ 5 u sur 20 m ; chaque croisement route × `EAUX` a un `PONTS` à moins de 20 m ;
  - `chemins` : BFS sur une grille de 10 m (arête marchable si pente ≤ 35°, ou si les deux cases sont sur le tablier d'un `PONTS`) : depuis le point juste devant chaque porte (repère local angle 270°, rayon `VILLAGE_R` + 30 m) on atteint (0, 0) et les 2 autres portes ;
  - `eau_descend` : pour chaque `EAUX`, `z_eau` décroissant le long de `pts` ; le lit (`height_at` au milieu) est sous `z_eau` ; chaque `CASCADES` a `z_haut - z_bas ≥ 120` u ;
  - `ravins_sortie` : pour chaque eau / ravin, depuis son fond on rejoint (BFS) un point hors de l'eau à chaque bout ;
  - `centre_plat` : sur le centre (`territoire == "centre"`), pente ≤ 5° ;
  - `relief_present` : dans chaque pays hors routes, l'écart max − min de hauteur est ≥ 15 m (600 u) à Konoha et à Suna, ≥ 40 m (1 600 u) à Kumo ;
  - `apparitions_plates` existant (adapter les rayons) ; `bord_infranchissable` existant inchangé.
- [ ] **Step 2 :** `python tools/test_relief.py` → les nouveaux tests échouent.
- [ ] **Step 3 :** implémenter (relief de fond par biome pondéré par `biome_poids`, aplanissement des routes et du centre, eaux / cascades / ponts / pics en données). Le temps de `python tools/relief.py` doit rester < 3 min (noter la mesure).
- [ ] **Step 4 :** `python tools/test_relief.py`, `python tools/test_carte.py` → OK ; écrire une vue du dessus (hauteurs ombrées + eaux + ponts + routes) dans `tools/out/vue-dessus.png` si Pillow est installé (facultatif, sinon ignorer) pour la relecture.
- [ ] **Step 5 :** commit `Relief de fond par biome, rivières, cascades, ponts et routes aplanies`.

### Task 3 : Villages agrandis à 700 m

**Files :**
- Modify : `tools/carte.py` (`TIERS`, `RAMPS` si besoin), `tools/plans-villages-v3.py` (positions, `SORTIES`, `HAUT_R0`, fenêtres de placement), `tools/plan-vers-blocage.py` (rayons codés en dur : rempart, porte, bornes, `BORDER`, `SPAWN_OFFS`, constantes en mètres ou en u qui supposent 500 m), `tools/test_plans.py`, `tools/test_relief.py` (rayons des tests), `tools/tests/*.cs` (valeurs codées en dur, recalées sans affaiblir les vérifications)

**Interfaces :**
- Consumes : `ECHELLE_VILLAGE`, `VILLAGE_R` (tâche 1), `height_at` (tâche 2).
- Produces : `TIERS = {"haut": (0, 238), "milieu": (238, 476), "bas": (476, 700)}` ; plans v3 au même format qu'avant (mêmes `code`, mêmes identifiants de domaine D1-D4 et de portes) ; `trous.holes` au même format.

- [ ] **Step 1 : tests qui échouent** dans `tools/test_plans.py` :
  - `TIERS["bas"][1] == 700` ;
  - chaque élément de surface d'un village (hors X*) est à ≤ 700 m − sa demi-taille du coin, et à ≥ 0 dans le quart (angles 225-315°) ;
  - chaque sortie X* est hors rempart (> 700 m) mais dans le pays (< 1 500 m − 50 m) et à ≥ 40 m d'une route ;
  - pour chaque bâtiment présent avant (comparer les codes à ceux générés par `git show HEAD:tools/plans-villages-v3.py`), même taille (w, h ou r) qu'avant ;
  - pas de chevauchement (test existant), distance aux rampes (test existant) ;
  - même liste de codes de portes et de domaines qu'avant.
- [ ] **Step 2 :** `python tools/test_plans.py` → échec.
- [ ] **Step 3 :** agrandir (positions × `ECHELLE_VILLAGE` dans le repère du coin, puis relancer le placement automatique dans les nouveaux anneaux). Ajouter **du relief dans les niveaux** (buttes / creux doux de 1 à 3 m) dans `relief.py`, à plat sous les bâtiments, places, rues et rampes (marge 6 m) — test ajouté dans `test_relief.py` : sous chaque élément « built » / « zone » / route du village, écart de hauteur ≤ 10 u.
- [ ] **Step 4 :** `python tools/test_plans.py`, `python tools/test_relief.py` (dont `souterrains_*`, `trous_sur_sorties`), `python tools/test_carte.py` → OK.
- [ ] **Step 5 :** commit `Villages agrandis à 700 m, relief doux dans les niveaux`.

### Task 4 : Blocage des territoires (routes, ponts, postes-frontière, contenu replacé)

**Files :**
- Modify : `tools/plan-vers-blocage.py`, `tools/test_relief.py` (ou `tools/test_blocage.py` à créer si plus clair)

**Interfaces :**
- Consumes : `ROUTES`, `EAUX`, `CASCADES`, `PONTS`, `PICS_KUMO`, `height_at` (tâche 2) ; `territoire` (tâche 1) ; plans agrandis (tâche 3).
- Produces : dans le script généré, un groupe racine par territoire `"<Territoire> (territoire)"` (Konoha, Suna, Kumo, 4e coin, Centre) contenant ses routes, ponts, postes-frontière et décor ; les groupes `"<Village> (blocage)"` restent tels quels (apparition par village).

- [ ] **Step 1 : tests qui échouent** (sur les boîtes générées, comme `souterrains_sans_intrusion` le fait déjà) :
  - chaque `PONTS` a un tablier + 2 garde-corps ; le tablier est au-dessus de l'eau (`z_eau` + 150 u au moins) et ses deux bouts touchent le sol (écart ≤ 20 u avec `height_at`) ;
  - un poste-frontière par route et par frontière traversée, à ≥ 3 m du bord de la chaussée, jamais à < 10 m d'un point d'apparition ;
  - les routes générées suivent `ROUTES` (le centre de chaque segment à ≤ 20 u de `height_at`) ;
  - les anciennes bornes à 900 m (`Borne`) ont disparu.
- [ ] **Step 2 :** lancer le test → échec.
- [ ] **Step 3 :** implémenter : routes en segments posés sur le relief, ponts, postes-frontière, eau en blocage (surfaces bleues translucides sans collision le long d'`EAUX`, bassins au pied des cascades, chutes = formes bleues translucides sans collision). Passe (b) de déplacement du contenu existant : la réécrire pour qu'elle soit **idempotente** (calcule la position cible depuis un repère stocké ou depuis l'ancienne position d'origine connue, jamais depuis la position actuelle) et qu'elle pose chaque objet sur `height_at` ; le contenu du centre (temple, marché, arènes…) doit tenir dans `territoire == "centre"`, celui du Pays du Feu dans le pays de Konoha hors village et hors routes.
- [ ] **Step 4 :** `python tools/plan-vers-blocage.py <plans> <tmp>/monde.cs.txt` sans erreur ; tous les tests Python OK ; `python tools/relief.py` régénère `tools/out/*`.
- [ ] **Step 5 :** commit `Blocage des territoires : routes, ponts, eau, postes-frontière, contenu replacé`.

### Task 5 : Végétation, arches et cascades (décor)

**Files :**
- Modify : `tools/plan-vers-blocage.py` (ou Create `tools/decor.py` appelé par lui, si le fichier devient trop gros), tests Python associés

**Interfaces :**
- Consumes : `territoire`, `biome_poids`, `dist_frontiere_m` (tâche 1) ; `height_at`, `ROUTES`, `EAUX`, `CASCADES`, `PONTS` (tâche 2) ; plans (tâche 3).
- Produces : groupes `"Végétation (<territoire>)"`, `"Arches (Suna)"` sous les groupes de territoire ; nombre total d'objets créés écrit dans l'en-tête du script généré.

- [ ] **Step 1 : tests qui échouent** :
  - aucun arbre / rocher à < 8 m d'une route, d'un pont, d'une eau, d'un point d'apparition, d'une sortie X*, d'un bâtiment ou d'une rampe ;
  - densité (arbres / hectare) : pays de Konoha ≥ 4× celle de Kumo ; Suna = 0 ; dans le village de Konoha, des arbres dans les parcs et le long des rues ;
  - 3 à 6 arches dans le pays de Suna, chacune avec un passage dessous ≥ 4 m de haut et ≥ 6 m de large, posées au sol des deux côtés ;
  - 4 à 8 cascades générées, chacune avec son bassin ;
  - nombre total d'objets du script ≤ 15 000 (aujourd'hui ~4 300) — sinon regrouper les arbres (un objet par bosquet).
- [ ] **Step 2 :** lancer → échec.
- [ ] **Step 3 :** implémenter (placement déterministe à graine fixe, arbres = tronc + boule / cône, sans collision pour les feuillages, collision pour les troncs).
- [ ] **Step 4 :** tests Python OK.
- [ ] **Step 5 :** commit `Décor : végétation par biome, arches de Suna, cascades`.

### Task 6 : Construction dans l'éditeur et vérifications en jeu

**Files :**
- Modify : `tools/terrain-import.cs.txt` (carte des sols par `biome_poids` : indices de matériau écrits dans `ControlMap`, damier si besoin ; trous inchangés), `Assets/scenes/konoha.scene`, `Assets/terrain/monde.terrain`, `docs/sbox-reference.md`, `docs/PROGRESS.md`
- Create : `tools/tests/ground-check.cs` (objets posés au sol), `tools/tests/path-check.cs` (facultatif si le BFS Python suffit — sinon un trajet en jeu porte → centre)

- [ ] **Step 1 :** écrire `tools/tests/ground-check.cs` : pour chaque objet des groupes `(territoire)` et des contenus replacés, une trace vers le bas depuis son point le plus bas + 200 u touche le sol à ≤ 40 u ; aucun objet entièrement sous le terrain. Renvoie `OK …` ou la liste des échecs.
- [ ] **Step 2 :** dans l'éditeur (scène `konoha.scene`, hors Play) : `python tools/plans-villages-v3.py <tmp>` → `python tools/plan-vers-blocage.py <tmp>` → `python tools/relief.py <tmp>` → `run_csharp tools/terrain-import.cs.txt` → `run_csharp docs/monde-blocage-v3.cs.txt` → **relancer** `docs/monde-blocage-v3.cs.txt` une 2e fois (idempotence : même nombre d'objets, mêmes positions du contenu replacé) → écrire la scène par le contournement (`docs/sbox-reference.md` §11) → vérifier la date du fichier. **Ne jamais modifier un prefab pendant un Play** (piège constaté : le cache de prefab de l'éditeur se corrompt, voir §11).
- [ ] **Step 3 :** `open_scene`, Play, lancer `spawn-check`, `border-check`, `hole-check`, `door-rules`, `door-check`, `domain-check`, `ground-check` → tous OK ; mesurer les i/s (≥ 55 min sur 2 s) dans les 3 villages et au cœur de la forêt de Konoha ; captures : vue d'ensemble du dessus, chaque village, la forêt, une arche, une cascade, un pont.
- [ ] **Step 4 :** `docs/sbox-reference.md` : faits vérifiés (`ControlMap`, matériaux, objets, piège du prefab modifié pendant le Play) ; `docs/PROGRESS.md` : tâche 14 mise à jour, « À tester par l'utilisateur » (parcourir les 4 territoires, passer une frontière, un pont, une cascade, une arche, la forêt, chaque village agrandi), nombre d'objets et i/s mesurés.
- [ ] **Step 5 :** relecture `relecteur` du générateur et des scripts ; corriger ; commit `Carte des territoires construite et vérifiée en jeu`.

### Task 7 : Couleurs du sol (biomes visibles)

**Files :**
- Modify : `Assets/terrain/*` (matériaux), `tools/terrain-import.cs.txt` si besoin, `docs/sbox-reference.md`, `docs/PROGRESS.md`

Contexte : les `.tmat` unis (16 px) s'affichent en gris sans leur couleur ; le compilateur a signalé des textures vides pour `roughness` / `normal` (`roche_tmat_bcr.generated.vtex`, `roche_tmat_nho.generated.vtex`, journal de l'éditeur du 2026-11-10 04:28). Piste à vérifier en premier.

- [ ] **Step 1 :** chercher dans le code source de s&box (dépôt public Facepunch) comment `TerrainMaterial` lit ses textures (albedo, normal, roughness…) et ce qu'il fait d'une texture absente ; noter la cause vérifiée.
- [ ] **Step 2 :** corriger les 4 matériaux (forêt, désert, roche, neutre — ajouter le 4e) ; relancer `terrain-import`.
- [ ] **Step 3 :** Play, captures du sol de chaque territoire et d'une frontière (fondu ou damier visible) ; vérifier que les 4 couleurs sont distinctes.
- [ ] **Step 4 :** `docs/sbox-reference.md` (cause + méthode), `docs/PROGRESS.md` ; relecture ; commit `Sol coloré par biome`.
