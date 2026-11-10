# Carte en coins, relief, niveaux et souterrains — plan de construction

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** remplacer la carte plate de 4,25 km par une carte de ~2,8 km avec un village en quart de cercle par coin, un relief infranchissable au bord, 3 niveaux par village, des souterrains, des portes fermées et l'attribution des domaines aux clans par le staff.

**Architecture :** tout le décor reste **généré** : des scripts Python (données en mètres) produisent (1) une carte de hauteurs pour un composant `Terrain` et (2) un script `run_csharp` de blocage ; on les lance dans l'éditeur, puis on écrit la scène avec le contournement connu. Côté jeu : un composant `LockedDoor` (ouverture décidée par l'hôte) et une table `domains` côté Worker pour lier un domaine à un clan.

**Tech Stack :** Python 3 (outils), s&box C# (Components, RPC, `[Sync(FromHost)]`, `Terrain`/`TerrainStorage`), Razor (panel F1), Cloudflare Worker TypeScript + D1 (`backend/`), `sbox-cli` pour piloter l'éditeur.

**Spec :** `docs/superpowers/specs/2026-11-10-carte-coins-relief-souterrains-design.md` (lire en entier avant de commencer).

## Global Constraints

- Code et commentaires en **français** ; noms de classes, méthodes, fichiers en anglais (CLAUDE.md).
- **Le serveur fait autorité** : le client envoie une intention (RPC), l'hôte vérifie et applique. Stats et états en `[Sync( SyncFlags.FromHost )]`.
- Toute API s&box absente de `docs/sbox-reference.md` est **vérifiée** (réflexion via `run_csharp` ou code source) puis **ajoutée** à la référence.
- Unités : **40 unités = 1 m**. Carte : demi-côté **56 000 u** (1 400 m) ; bande de relief **6 000 u** (150 m) ; zone jouable `|x|,|y| ≤ 50 000`.
- Coins des villages (point intérieur du coin, origine du repère local) : **Konoha (-50000, 50000)**, **Suna (50000, 50000)**, **Kumo (50000, -50000)**, **4e coin (-50000, -50000)** (relief + sol neutre seulement, rien d'autre).
- Repère local d'un village : origine au coin, **-Y vers le centre de la carte** (convention actuelle des plans). Lacet du groupe racine : `yaw = atan2(-cx, cy)` en degrés (Konoha → 45°). Le quart de cercle occupe les angles locaux **225° à 315°** ; grande porte à **270°, r = 500 m**.
- Niveaux (rayon depuis le coin) : **haut < 170 m**, **milieu 170–340 m**, **bas 340–500 m**. Hauteurs : bas **0**, milieu **+320 u**, haut **+800 u** ; Kumo ×2 (**+640**, **+1600**). 2 ou 3 rampes marchables par marche, ailleurs pente non marchable.
- Pays de chaque village : jusqu'à **900 m** du coin ; frontière = arc de bornes à 900 m. Centre commun : disque de **375 m** (15 000 u) à l'origine, inchangé.
- Noms des groupes racines gardés : **« Konoha (blocage) », « Suna (blocage) », « Kumo (blocage) »** (`GameManager.PickSpawn` s'en sert).
- Lieux fermés : règle `Kage`, `Council`, `SpecialUnit`, `Police` → **ouverts au Kage du village (grade `kage`, même village) et au staff** tant que les rôles n'existent pas ; règle `Clan` → membres du clan propriétaire du domaine (`clan` égal **et** `ClanValidated`, même village) + staff ; domaine sans clan → staff seul.
- Porte : se referme seule après **4 s** ; suivre quelqu'un est toléré ; porte tunnel ↔ pièce **ouvrable seulement côté pièce**.
- Performances : rester à **≥ 55 i/s minimum** en Play dans l'éditeur (aujourd'hui ~60, ~4 000 objets).
- Pas de secrets dans le dépôt. Commit après chaque tâche, message en français, avec la ligne `Co-Authored-By` en vigueur.

## Review Focus

1. **Joueur qui apparaît coincé** (dans un bloc, sous le terrain, dans une pente) → chaque point d'apparition doit passer une trace de boîte 32×32×72 libre et toucher le sol à moins de 30 u en dessous. Test : Task 6 `spawn-check.cs`.
2. **Sortir de la carte** par le bord (course ninja + bond, Shunshin vers l'extérieur) → impossible : mur invisible sur tout le pourtour intérieur. Test : Task 1 `test_relief.py::bord_infranchissable` (hauteurs) + Task 6 `border-check.cs` (traces vers l'extérieur sur les 4 côtés touchent le mur).
3. **Shunshin ou bond à travers une porte fermée ou un plafond de souterrain** → le joueur reste du même côté. Test : Task 4 `door-check.cs` (Shunshin face à une porte fermée).
4. **Entrée cachée sans sortie** : on tombe dans un trou du terrain et on ne peut plus remonter → chaque sortie cachée a un accès marchable dans les deux sens. Test : Task 6 `hole-check.cs`.
5. **Domaine attribué à un clan non validé, d'un autre village, ou à un clan inexistant** → refus 409 du Worker. Test : Task 5 `smoke.mjs` « domaine : clan non validé ou autre village → 409 ».

---

## Structure des fichiers

| Fichier | Rôle |
|---|---|
| `tools/carte.py` (nouveau) | Constantes partagées (tailles, coins, niveaux, styles) et `local_to_world`, `height_at(x, y)` |
| `tools/relief.py` (nouveau) | Construit la carte de hauteurs + trous → `tools/out/relief.u16`, `tools/out/holes.u8`, `tools/out/relief.json` |
| `tools/test_relief.py` (nouveau) | Tests du relief (asserts, `python tools/test_relief.py`) |
| `tools/plans-villages-v3.py` (nouveau, à partir de v2) | Plans des 3 villages en quart de cercle, avec `level`, `layer`, `access` |
| `tools/plan-vers-blocage.py` (modifié) | Lit v3 + `carte.py`, place tout à la bonne hauteur, souterrains, portes, déplacement des groupes du monde |
| `tools/terrain-import.cs.txt` (nouveau) | Script `run_csharp` : charge `tools/out/*` dans `Assets/terrain/monde.terrain` et pose le `Terrain` |
| `tools/tests/*.cs` (nouveaux) | Scripts de test `run_csharp` (apparition, bord, portes, trous) |
| `Code/World/DoorRules.cs` (nouveau) | Règle d'accès pure, testable |
| `Code/World/LockedDoor.cs` (nouveau) | Porte réseau : ouverture par l'hôte, fermeture auto, sens unique |
| `Code/World/DomainRegistry.cs` (nouveau) | Hôte : domaine → clan, chargé depuis l'API |
| `backend/migrations/0003_domains.sql`, `backend/src/index.ts`, `backend/test/smoke.mjs` | Table et routes des domaines |
| `Code/Network/Api/IApiClient.cs`, `WorkerApiClient.cs`, `MemoryApiClient.cs`, DTO | Méthodes domaines |
| `Code/Staff/StaffCommands.cs`, `Code/UI/StaffPanel.razor` | Action « domaine » dans F1 |
| `.gitignore` | ajouter `tools/out/` |

---

### Task 1 : Relief (carte de hauteurs) et constantes de carte

**Files :** Create `tools/carte.py`, `tools/relief.py`, `tools/test_relief.py` ; Modify `.gitignore`.

**Interfaces :**
- Produces (`tools/carte.py`) : `HALF = 56000`, `BAND = 6000`, `INNER = 50000`, `M = 40`, `CORNERS = {"konoha": (-50000, 50000), "suna": (50000, 50000), "kumo": (50000, -50000), "libre": (-50000, -50000)}`, `TIERS = {"haut": (0, 170), "milieu": (170, 340), "bas": (340, 500)}` (mètres), `TIER_Z = {"konoha": (0, 320, 800), "suna": (0, 320, 800), "kumo": (0, 640, 1600)}` (bas, milieu, haut), `RAMPS = {"konoha": [250, 290], "suna": [245, 270, 295], "kumo": [260, 280]}` (angles locaux des rampes, degrés), `yaw_of(v) -> float`, `local_to_world(v, x_m, y_m) -> (x_u, y_u)`, `world_to_local(v, x_u, y_u) -> (x_m, y_m)`, `zone_of(x_u, y_u) -> str` (`"bord-<v>"`, `"village-<v>"`, `"pays-<v>"`, `"centre"`, `"neutre"`).
- Produces (`tools/relief.py`) : `RES = 2048`, `TERRAIN_Z = -2000` (le terrain est posé à z = -2000 u, sol normal = hauteur 2000 → z monde 0), `TERRAIN_HEIGHT = 40000`, `build() -> (heights: list[int] (RES*RES, 0..65535), holes: list[bool], meta: dict)`, `height_at(x_u, y_u) -> float` (z monde, interpolé), écrit `tools/out/relief.u16` (little-endian, ligne y puis x, origine coin (-56000,-56000)), `tools/out/holes.u8`, `tools/out/relief.json` (`{"res", "size": 112000, "height": 40000, "z": -2000}`).

Hauteurs du bord (z monde, au sommet de la bande, montée sur les 60 premiers mètres) : Konoha **2400**, Suna **2000**, Kumo **6000**, libre **2000** ; chaque demi-côté prend le style du coin le plus proche (`zone_of`). Route village → centre et centre commun : hauteur 0. Trous : liste `HOLES` (centre en mètres local + taille), remplie en Task 3 ; vide ici.

- [ ] **Step 1 : écrire `tools/test_relief.py`** avec ces tests (fonctions appelées par `if __name__ == "__main__"`, chacune affiche `OK <nom>`) :
  - `bord_infranchissable` : pour 400 points répartis sur le pourtour à `INNER + 3000`, `height_at ≥ 1900` ; et pour tout point avec `|x| ou |y| > INNER + 2400`, la pente vers l'intérieur dépasse 45°.
  - `niveaux_village` : pour chaque village, `height_at` au point local (angle 270°, r = 420 m) == bas, (270°, 250 m) == milieu, (240°, 80 m) == haut (tolérance 5 u).
  - `rampes_marchables` : le long de chaque rampe (angle de `RAMPS`), pente ≤ 35° entre r = 100 m et r = 360 m ; hors rampe (angle + 8°), la marche bas→milieu a une pente > 50°.
  - `centre_et_routes_plats` : `height_at(0,0)` == 0 ; 50 points sur chaque route porte → bord du centre à 0 (±5).
  - `apparitions_plates` : 4 points à ±500 u autour de la place (270°, 420 m) de chaque village, variation de hauteur < 10 u.
- [ ] **Step 2 :** `python tools/test_relief.py` → échoue (`ModuleNotFoundError: relief`).
- [ ] **Step 3 :** implémenter `carte.py` et `relief.py` (numpy interdit : pas de dépendance à installer ; listes Python + `array('H')`). Lisser les marches entre niveaux sur 6 m, sauf rampes sur 25 m.
- [ ] **Step 4 :** `python tools/test_relief.py` → tous `OK`. `python tools/relief.py` écrit les 3 fichiers.
- [ ] **Step 5 :** commit (`tools/carte.py tools/relief.py tools/test_relief.py .gitignore`).

### Task 2 : Plans v3 en quart de cercle

**Files :** Create `tools/plans-villages-v3.py` (part de `tools/plans-villages-v2.py`, mêmes listes de bâtiments) ; Test : `tools/test_plans.py`.

**Interfaces :**
- Consumes : `carte.TIERS`, `carte.RAMPS`.
- Produces : un JSON par élément dans `<sortie>/`, même format que v2 (`village`, `code`, `name`, `group`, `fill`, `shape` en mètres, repère local) **plus** `level` (`"bas"|"milieu"|"haut"`), `layer` (`"surface"|"souterrain"`), et pour les portes `door: {"rule": "Kage|Council|SpecialUnit|Police|Clan", "domain": "<village>-<code>"|null, "oneWay": bool}`. Rempart `A1` : `RING(0, 0, 500, w, 225, 315)` ; porte à 270°. Nouvelles formes : aucune (on garde `circle`, `rect`, `ring`, `line`, `houses`).

Placement : pouvoir (palais, conseil, archives, bureau des missions) au niveau **haut** ; académie, hôpital, entraînement, habitations, domaines, police, caserne au **milieu** ; place d'apparition `V6`, rue commerçante, boutique, garde, ramen au **bas**. Placement automatique sans chevauchement comme v2, mais dans l'anneau du niveau et entre 228° et 312°, à au moins 12 m des rampes. À Konoha, falaise des Kage (5 emplacements) dans le relief derrière le palais.

Souterrains (`layer: "souterrain"`, z sous le sol, géré en Task 3) par village : `S1` archives interdites sous le palais (porte `Kage`) ; `S2` salle unité spéciale sous `U3` (`SpecialUnit`) ; `S3` cellules sous `U2` (`Police`) ; `S4..` un refuge sous chaque domaine (`Clan`, `domain` = id du domaine) ; `T1` tunnel archives → sortie cachée `X1` hors rempart (porte `S1`↔`T1` : `Kage`, `oneWay: true`) ; `T2` tunnel salle unité → sortie `X2` (`SpecialUnit`, `oneWay: true`) ; `H1` un passage caché entre bas et milieu (ouvert, pas de porte). Sorties `X1`, `X2` : dans le pays, entre 560 et 800 m du coin, jamais sur une route.

- [ ] **Step 1 : `tools/test_plans.py`** : génère dans un dossier temporaire puis vérifie : chaque élément de surface est dans le quart (angle 225–315°, r ≤ 500 m sauf `X*` et falaise) ; aucun chevauchement entre bâtiments de surface ; chaque élément a `level` cohérent avec son rayon (`TIERS`) ; chaque `S*` a une porte ; chaque domaine a exactement un refuge avec `domain` = son id ; `X1`, `X2` à 560–800 m ; `V6` au niveau bas.
- [ ] **Step 2 :** lancer → échec (script absent).
- [ ] **Step 3 :** écrire `plans-villages-v3.py`.
- [ ] **Step 4 :** `python tools/test_plans.py` → OK.
- [ ] **Step 5 :** mettre à jour la page des plans (https://claude.ai/artifact/7pKR4Tj3yL5A21MUd7kkeG, collection `elements`) avec les éléments v3 via `ArtifactData` `batch` (supprimer les v2 du même village d'abord). Si la page n'affiche pas un quart de cercle correctement, la mettre à jour (anneau partiel déjà supporté par `ring a0/a1`).
- [ ] **Step 6 :** commit.

### Task 3 : Générateur de blocage sur le relief

**Files :** Modify `tools/plan-vers-blocage.py` ; Create `tools/terrain-import.cs.txt` ; Modify `tools/relief.py` (liste `HOLES` lue depuis les plans v3) ; Output `docs/monde-suna-kumo.cs.txt` (renommer en `docs/monde-blocage-v3.cs.txt`, mettre à jour les mentions dans `docs/PROGRESS.md`).

**Interfaces :**
- Consumes : `carte.*`, `relief.height_at`, éléments v3.
- Produces : script `run_csharp` qui (a) supprime les groupes « Konoha/Suna/Kumo (blocage) », « Vue d'ensemble (blocage) », l'objet « Sol » du monde (grand bloc), « Frontière du Pays du Feu » ; (b) déplace « Pays du Feu - zone de Konoha » et « Terres neutres de Konoha » de l'ancien repère de Konoha (position (-60000, 0, 0), lacet 90°) vers le nouveau (coin Konoha, `yaw_of("konoha")`), puis détruit leurs enfants qui tombent dans le village (r < 520 m du coin), dans la bande de relief, hors carte ou sur une route ; (c) recrée les 3 villages : chaque élément posé à `z = height_at(...)` de son centre ; (d) frontière = arc de bornes à 900 m pour les 3 villages + poste-frontière sur la route ; (e) souterrains : pièces et tunnels en blocs (sol, murs, plafond) dont le **plafond est sous le terrain d'au moins 60 u** ; (f) portes : un objet « Porte <code> » avec `LockedDoor` (propriétés de Task 4) ; (g) 4 murs invisibles (`BoxCollider` sans rendu) le long de `|x|,|y| = INNER + 2400`, hauteur 0 → 20 000 ; (h) 4 points d'apparition par village sur `V6`, `Enabled = true`.
- `tools/terrain-import.cs.txt` : crée ou réutilise `terrain/monde.terrain` (`Editor.AssetSystem.FindByPath`, sinon outil `create_asset` avant), `SetResolution(2048)`, `TerrainSize = 112000`, `TerrainHeight = 40000`, copie `relief.u16` dans `HeightMap`, trous dans `ControlMap` via `new CompactTerrainMaterial( 0, 0, 0, true ).Packed`, `asset.SaveToDisk( storage )`, pose « Relief » (`Terrain`, `Storage` = l'asset) en `(-56000, -56000, -2000)`. Vérifié par l'essai du 2026-11-10 : un `TerrainStorage` non enregistré comme asset est sérialisé `"Storage": null` — **il faut l'asset**.

Styles (spec) : Konoha buttes douces (marches lissées plus larges, 12 m), Suna terrasses de roche (marches nettes + murets de bord de terrasse en blocs couleur roche), Kumo pics (marches raides) avec **2 ponts** en blocs entre le milieu et le haut au-dessus de la marche. Couleur du sol par zone : vérifier d'abord le rendu du `Terrain` sans matériau ; s'il est neutre, créer un `.tmat` uni par zone (forêt, désert, roche grise, neutre) et peindre `ControlMap` selon `zone_of` dans `terrain-import.cs.txt` (vérifier `TerrainMaterial` et `Storage.Materials` par réflexion, noter dans la référence).

- [ ] **Step 1 :** ajouter à `tools/test_relief.py` : `trous_sur_sorties` (chaque `X*` des plans v3 est un trou dans `holes`, et aucun trou ailleurs) et `souterrains_sous_terrain` (pour chaque pièce/tunnel, plafond ≤ `height_at - 60` en tout point de son emprise ; un tunnel qui passe sous un changement de niveau suit la plus basse hauteur).
- [ ] **Step 2 :** lancer → échec.
- [ ] **Step 3 :** implémenter (générateur + `HOLES` + `terrain-import.cs.txt`). Chaque sortie `X*` : puits en pente marchable (≤ 35°) du trou jusqu'au tunnel.
- [ ] **Step 4 :** tests Python OK ; `python tools/plans-villages-v3.py <dossier>` puis `python tools/plan-vers-blocage.py <dossier>` écrivent le script.
- [ ] **Step 5 :** commit (sans la scène : elle est écrite en Task 6).

### Task 4 : Portes fermées (`DoorRules`, `LockedDoor`)

**Files :** Create `Code/World/DoorRules.cs`, `Code/World/LockedDoor.cs`, `tools/tests/door-rules.cs`, `tools/tests/door-check.cs`.

**Interfaces :**
- Produces :
  - `namespace NarutoRp.World; public enum DoorRule { Kage, Council, SpecialUnit, Police, Clan, Staff }`
  - `public static class DoorRules { public static bool CanOpen( DoorRule rule, string doorVillage, string domainClan, string village, string grade, string clan, bool clanValidated, string staffRank ) }` — staff (`StaffRank` non vide) → vrai ; `Kage`/`Council`/`SpecialUnit`/`Police` → `grade == "kage" && village == doorVillage` ; `Clan` → `domainClan` non vide, `clan == domainClan`, `clanValidated`, même village ; `Staff` → staff seul.
  - `public sealed class LockedDoor : Component` : `[Property] DoorRule Rule`, `[Property] string Village`, `[Property] string DomainId`, `[Property] bool OneWay` (côté autorisé = `WorldRotation.Forward`), `[Sync( SyncFlags.FromHost )] bool IsOpen`, `[Sync( SyncFlags.FromHost )] string OwnerClan` (rempli par l'hôte depuis `DomainRegistry`, Task 5), `[Rpc.Host] void RequestOpen()` (lit `Rpc.Caller`, retrouve son `NinjaPlayer`, refuse si > 200 u, si `OneWay` et joueur du mauvais côté, ou si `DoorRules.CanOpen` faux ; sinon `IsOpen = true` pendant 4 s). Le battant (enfant « Battant » avec `ModelRenderer` + `BoxCollider`) est actif quand `IsOpen` est faux, chez tous les clients.
  - Côté client : touche **`Use`** en regardant la porte (trace caméra ≤ 150 u) → `RequestOpen()`. Ne pas gêner `ReviveAction` (qui ne réagit qu'à un ninja à terre).
- Vérifier et noter dans `docs/sbox-reference.md` : qu'un `[Rpc.Host]` d'instance sur un objet de scène appartenant à l'hôte est appelable par n'importe quel client, et que `[Sync(FromHost)]` sur un objet de scène se synchronise (mode réseau de l'objet).

- [ ] **Step 1 : `tools/tests/door-rules.cs`** (`run_csharp`, renvoie `"OK"` ou la liste des échecs) : Kage même village + `Council` → vrai ; Kage autre village → faux ; jônin + `Police` → faux ; staff `moderator` + `Clan` sans propriétaire → vrai ; clan égal non validé → faux ; clan égal validé même village → vrai ; clan égal validé autre village → faux ; `Clan` avec `domainClan` vide et joueur non staff → faux.
- [ ] **Step 2 :** lancer → erreur de compilation (type absent).
- [ ] **Step 3 :** implémenter `DoorRules` et `LockedDoor`.
- [ ] **Step 4 :** `door-rules.cs` → `OK`.
- [ ] **Step 5 : `tools/tests/door-check.cs`** (en Play, dans une petite scène de test ou devant une porte de la carte) : porte fermée → un joueur non autorisé qui appelle `RequestOpen` la laisse fermée ; joueur staff → `IsOpen` vrai puis faux après ~4 s ; `OneWay` : demande depuis le mauvais côté refusée ; **Shunshin** lancé face à la porte fermée à 3 m → le joueur reste du même côté (signe du produit scalaire inchangé).
- [ ] **Step 6 :** relecture par le sous-agent `relecteur` ; corriger ; commit.

### Task 5 : Domaines de clan (base, API, F1)

**Files :** Create `backend/migrations/0003_domains.sql`, `Code/World/DomainRegistry.cs` ; Modify `backend/src/index.ts`, `backend/test/smoke.mjs`, `Code/Network/Api/IApiClient.cs` (+ `WorkerApiClient.cs`, `MemoryApiClient.cs`, DTO à côté des autres), `Code/Core/GameManager.cs`, `Code/Staff/StaffCommands.cs`, `Code/UI/StaffPanel.razor`.

**Interfaces :**
- SQL : `CREATE TABLE domains ( id TEXT PRIMARY KEY, village TEXT NOT NULL CHECK (village IN ('konoha','suna','kumo')), clan TEXT, assigned_by TEXT, assigned_at INTEGER );`
- Worker : `GET /v1/domains` → `[{ id, village, clan }]` ; `POST /v1/domains/:id/clan` corps `{ clan: string|null, village, staffSteamId }` → `requireStaff(env, staffSteamId, 'clan')` ; `id` doit commencer par `village + "-"` (400 sinon) ; `clan` non nul doit être le clan **validé** d'au moins un perso vivant du **même village** (409 sinon) ; upsert ; log `domain_assigned` `{ domainId, clan }`.
- C# : `public sealed class DomainDto { public string Id; public string Village; public string Clan; }` ; `Task<List<DomainDto>> GetDomainsAsync( CancellationToken ct = default )` ; `Task SetDomainClanAsync( string domainId, string village, string clan, ulong staffSteamId, CancellationToken ct = default )`.
- `public static class DomainRegistry { public static string ClanOf( string domainId ); public static void Set( string domainId, string clan ); public static Task LoadAsync( IApiClient api ); }` (hôte seulement) ; après chargement ou `Set`, l'hôte met à jour `OwnerClan` de chaque `LockedDoor` dont `DomainId` correspond. `LockedDoor.RequestOpen` passe `OwnerClan` comme `domainClan`.
- `StaffCommands.RequestSetDomainClan( string domainId, string village, string clan )` : `Allowed( StaffAction.Clan )`, puis `RunApi( api => api.SetDomainClanAsync(...), message, () => DomainRegistry.Set(...) )` comme `RequestValidateClan`.
- F1 : section « Domaines » : liste des domaines de la scène (portes `Rule == Clan`, `DomainId` distincts) avec leur `OwnerClan` ; pour un domaine : champ clan + « Attribuer », bouton « Retirer » (clan nul).

- [ ] **Step 1 :** tests `smoke.mjs` : « domaine : modérateur sans droit clan → 403 » (suivre la règle `requireStaff` existante) ; « domaine : clan non validé ou autre village → 409 » ; « domaine : attribuer puis lister → clan présent » ; « domaine : retirer → clan null » ; « domaine : id d'un autre village → 400 ».
- [ ] **Step 2 :** `npm test` dans `backend/` → les nouveaux tests échouent.
- [ ] **Step 3 :** migration + routes.
- [ ] **Step 4 :** `npm test` → tout passe (anciens tests compris).
- [ ] **Step 5 :** côté jeu : API, `MemoryApiClient` (accepte tout clan non vide), `DomainRegistry` chargé au démarrage de l'hôte (échec API → log + domaines vides, le jeu continue : seuls staff/Kage entrent), commande staff, panel.
- [ ] **Step 6 :** test en Play (API mémoire) : attribuer un domaine au clan d'un perso validé → la porte du refuge s'ouvre pour lui, pas pour un autre ; retirer → refusé. Script `tools/tests/domain-check.cs`.
- [ ] **Step 7 :** relecture `relecteur` ; corriger ; commit.

### Task 6 : Construction de la carte et vérifications en jeu

**Files :** Create `tools/tests/spawn-check.cs`, `tools/tests/border-check.cs`, `tools/tests/hole-check.cs` ; Modify `Assets/scenes/konoha.scene`, `Assets/terrain/monde.terrain` (+ fichiers de données générés à côté, à committer s'ils ne sont pas ignorés), `docs/PROGRESS.md`, `docs/sbox-reference.md`.

- [ ] **Step 1 :** écrire les 3 scripts de test :
  - `spawn-check.cs` : pour chaque `SpawnPoint`, trace de boîte 32×32×72 depuis +10 u → libre, et rayon vers le bas touche le sol à < 30 u ; 4 points par village (12).
  - `border-check.cs` : depuis 40 points au bord intérieur de chaque côté (z = sol + 100 et sol + 3000), trace vers l'extérieur sur 8 000 u → touche toujours un collider avant `|x|,|y| = HALF`.
  - `hole-check.cs` : pour chaque sortie `X*`, un joueur posé au bord du trou et poussé dedans finit sous z = -100 (dans le tunnel), puis une trace de capsule le long du puits remonte jusqu'à la surface sans obstacle.
- [ ] **Step 2 :** dans l'éditeur (`sbox-cli`, scène `konoha.scene` ouverte, hors Play) : `create_asset` `terrain/monde.terrain` si absent → `run_csharp tools/terrain-import.cs.txt` → `run_csharp docs/monde-blocage-v3.cs.txt` → écrire la scène avec le contournement (`docs/sbox-reference.md` §11 : `scene.Serialize()["GameObjects"]` réécrit dans le `.scene`) → vérifier la date du fichier.
- [ ] **Step 3 :** recharger la scène (`open_scene`), Play, lancer les 3 scripts → tous OK ; créer un perso par village (libérer les emplacements de l'API mémoire si besoin) → apparition sur sa place ; mesurer les i/s (≥ 55 min sur 2 s) ; captures d'écran des 3 villages et du bord.
- [ ] **Step 4 :** noter dans `docs/sbox-reference.md` les faits vérifiés sur `Terrain` (`TerrainStorage.SetResolution`, `HeightMap` ushort, `ControlMap` + `CompactTerrainMaterial.IsHole`, asset obligatoire, `SaveToDisk`), et tout ce qui a été vérifié en Task 4.
- [ ] **Step 5 :** relecture `relecteur` du générateur et des scripts ; corriger.
- [ ] **Step 6 :** `docs/PROGRESS.md` : tâche 14 mise à jour (carte en coins, relief, souterrains, portes, domaines), « À tester par l'utilisateur » (visiter les 3 villages, les escaliers, une sortie cachée, une porte), prochaine tâche = zones de missions. Commit.
