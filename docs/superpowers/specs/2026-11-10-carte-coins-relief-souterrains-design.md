# Carte en coins, relief, niveaux et souterrains — conception

Date : 2026-11-10. Statut : relu (5 questions traitées), en attente de validation par l'utilisateur.
Repères visuels : `.superpowers/brainstorm/…/content/carte-complete.html` (carte complète proposée).

## Pourquoi

Aujourd'hui la carte fait 4,25 km de côté, les villages sont des disques de ~480 m à 1,5 km du centre, et tout est plat. L'utilisateur trouve la carte **trop grande pour des villages trop petits**, avec des bords et des coins vides. Il veut :

- se servir des bords de la carte : les villages vont **dans les coins** et **épousent le coin** ;
- du **relief** au bord de la carte, différent selon la zone ;
- des **niveaux** dans les villages, avec le **palais du Kage en hauteur** ;
- des **souterrains** : des pièces secrètes sous les bâtiments **et** des tunnels qui sortent du village ;
- des zones **plus ou moins cachées** : certaines fermées à certains joueurs, d'autres ouvertes à qui les trouve.

Le but RP : des villages qui se défendent par leur forme (dos au relief, une porte face au centre, des escaliers à garder), des lieux de pouvoir en hauteur, et des secrets à découvrir ou à protéger (évasions, infiltrations, réunions discrètes).

## Décidé par l'utilisateur

1. **Carte réduite**, villages gardés à peu près à leur taille actuelle (option « B » montrée).
2. **Un village par coin**. Le **4e coin** (bas-gauche) est réservé, **son contenu sera décidé plus tard** : ne rien y construire à part le relief du bord et un sol neutre.
3. **Centre commun gardé** au milieu.
4. Chaque village a la forme d'un **quart de cercle** collé dans son coin : les deux bords de la carte (relief) forment le fond, le rempart arrondi fait face au centre.
5. **Relief au bord de la carte**, qui correspond à chaque zone.
6. Villages **à niveaux**, **palais du Kage en hauteur**.
7. Souterrains : **pièces secrètes ET tunnels qui sortent du village**.
8. Accès : **un mélange** — certains lieux fermés selon le joueur, d'autres simplement cachés.

## Proposé par Claude (l'utilisateur peut changer)

### Carte
- Carré de **~2 800 m** de côté (112 000 unités). Centre commun : disque de 375 m au milieu (inchangé).
- **Konoha haut-gauche, Suna haut-droite, Kumo bas-droite**, 4e coin bas-gauche.
- **Relief du bord** : bande d'environ **150 m** de large tout autour, **infranchissable** (pentes trop raides ou falaises + mur invisible au sommet). Chaque moitié de bord prend le style du coin le plus proche : collines boisées hautes et falaises (Konoha), falaises de grès et dunes (Suna), montagnes et pics (Kumo), relief neutre rocheux autour du 4e coin.
- Autour de chaque village, son **pays** (zone de ~900 m depuis le coin) garde le rôle actuel : forêt et clairières de missions pour le Feu, désert pour le Vent, montagne pour la Foudre. Entre les pays et le centre : **terres neutres**. Une **route** relie chaque grande porte au centre commun.
- Le contenu actuel des terres neutres et du Pays du Feu (forêt, 3 clairières, frontière, poste-frontière, ruines, ressources, temple, marché, arènes) est **gardé et replacé** dans la nouvelle géométrie, pas supprimé.

### Village en quart de cercle
- Rayon du rempart **~500 m** depuis le coin intérieur (même surface que les villages ronds actuels, à ±10 %).
- **Grande porte** au milieu de l'arc, face au centre de la carte. Le rempart garde le style de chaque village (mur de bois/pierre à Konoha, falaises en cratère à Suna, pics à Kumo).
- **3 niveaux**, qui montent de la porte vers le fond du coin :
  - **Bas** (de ~340 à 500 m du coin) : place d'apparition, rue commerçante, boutique, poste de garde, auberge/ramen.
  - **Milieu** (~170 à 340 m) : quartiers d'habitation, académie, hôpital, terrains d'entraînement, domaines de clans, police, caserne.
  - **Haut** (moins de ~170 m) : palais du Kage, salle du conseil, archives, bureau des missions. À Konoha, la **falaise des Kage** (5 emplacements de visages) est derrière le palais, dans le relief du coin.
  - Écart de hauteur indicatif : ~8 m entre bas et milieu, ~12 m entre milieu et haut ; Kumo plus raide (jusqu'à ~2×).
  - Passages entre niveaux : **peu nombreux** (2 ou 3 escaliers/rampes par marche), faciles à garder en RP. Les pentes entre niveaux sont trop raides pour être montées en marchant hors de ces passages (on peut quand même les franchir avec le bond de la course ninja ou le Shunshin : c'est voulu, c'est un jeu de ninjas).
- Style des niveaux : buttes douces et vertes (Konoha), terrasses taillées dans la roche (Suna), pics reliés par des ponts (Kumo).
- Les bâtiments des **plans v2** (`tools/plans-villages-v2.py`) sont **replacés** dans le quart de cercle selon la règle ci-dessus (pouvoir en haut, vie quotidienne en bas). Liste de bâtiments inchangée.

### Souterrains et lieux cachés
Par village :

| Lieu | Où | Accès |
|---|---|---|
| Archives interdites + départ du tunnel d'évacuation | sous le palais | **fermé** : Kage, conseil, staff |
| Salle de l'unité spéciale | sous son QG caché | **fermé** : membres de l'unité, staff |
| Cellules | sous le QG de la police | **fermé** : police, garde, staff ; un prisonnier enfermé ne sort pas seul |
| Refuge de clan | sous chaque domaine de clan | **fermé** : membres du clan, staff |
| Tunnels | relient certaines pièces entre elles et sortent **hors du rempart** | — |
| 1 ou 2 sorties de tunnel | grotte dans le relief, vieux puits dans le pays | **caché** : ouvert à qui le trouve |
| 1 ou 2 passages entre quartiers ou niveaux | ruelle derrière une cascade, toits, faille | **caché** : ouvert à qui le trouve |

- Style : galeries en terre étayées de bois (Konoha), couloirs taillés dans la roche (Suna), grottes de montagne (Kumo).
- Une sortie cachée débouche à l'**extérieur** du rempart : c'est volontaire (évasions, infiltrations). Elle doit être difficile à repérer, pas impossible.
- Le **staff ouvre tout** (comme le reste du panel F1).
- **Règles d'accès fixes au départ** (dans le code ou les données du blocage). Les rendre modifiables en jeu par le staff est une suite possible, **hors de ce projet**.

## Contraintes

- **s&box** : le relief se fait avec le composant **`Terrain`** (carte de hauteurs). Une carte de hauteurs ne fait **pas de surplomb** : les souterrains sont donc de la géométrie à part, posée **sous** le terrain, et on y entre par un **trou** dans le terrain (`TerrainMaterialInfo.IsHole` existe dans l'API, **à vérifier** avant de s'en servir) ou par une entrée dans un bâtiment. Toute API non listée dans `docs/sbox-reference.md` doit être vérifiée et ajoutée à la référence (règle du projet).
- **Chaîne actuelle** à garder : plans 2D (page des plans + `tools/plans-villages-v2.py`, mètres, repère local du village) → `tools/plan-vers-blocage.py` → script `run_csharp` → scène `konoha.scene` écrite avec le contournement de sauvegarde (`docs/sbox-reference.md` §11). Le format des plans doit gagner : la forme « quart de cercle », une **hauteur de niveau** par élément, et une **couche souterraine**.
- **Apparition par village** (`GameManager.PickSpawn`) : repose sur le nom du groupe racine « <Village> (blocage) ». À garder, ou à adapter dans le même changement.
- **Serveur qui fait autorité** : une porte fermée est un obstacle physique que **seul l'hôte** ouvre, après vérification du joueur. Rappel (PROGRESS) : le client contrôle son propre déplacement, l'hôte ne peut pas le déplacer → la porte doit être un vrai collider côté client, synchronisé par l'hôte.
- **Performances** : aujourd'hui ~4 000 objets à ~60 i/s. Ne pas faire baisser ça de façon visible ; le terrain doit remplacer des blocs de sol, pas s'y ajouter.
- Serveur unique (< 64 joueurs) sur une seule carte : inchangé.
- **Pas de mort automatique**, pas de dégâts de chute qui tuent (si les chutes de niveau font mal, elles mettent au plus à terre — à décider par le constructeur, voir plus bas).

## Hors de ce projet (non-objectifs)

- Le contenu du **4e coin**.
- Les **zones de missions** autour de Suna / Kumo (tâche suivante, posée sur ce relief).
- Les vrais modèles 3D, textures et effets : on reste en **blocage** (formes simples colorées), mais avec du vrai relief.
- Un système de **rôles** (conseil, unités, police, garde) : plus tard. En attendant, voir « Réponses après relecture ».
- Le réglage des accès **en jeu** par le staff.

## À éviter absolument (anti-objectifs)

- Un village qui redevient « un rond posé dans un coin » : il doit **épouser** le coin, dos au relief.
- Un relief **franchissable** au bord : on ne doit jamais tomber hors de la carte ni la contourner.
- Une pièce **fermée** dans laquelle un joueur non autorisé peut entrer (trou dans le décor, bond, Shunshin à travers un mur, apparition dedans). Le Shunshin s'arrête déjà avant les murs : vérifier qu'il ne traverse ni une porte fermée ni le plafond d'un souterrain.
- Une sortie cachée **visible de loin** ou indiquée sur le HUD.
- Un joueur qui **apparaît coincé** (dans un bloc, sous le terrain) : déjà arrivé une fois, à tester systématiquement (trace sur chaque point d'apparition).

## Laissé au constructeur

- Les tailles exactes (largeur du relief, hauteurs de niveaux, rayons), tant que l'esprit ci-dessus est gardé.
- La résolution du terrain et la façon de le générer (depuis le script Python, peint, ou mixte).
- Le tracé exact des tunnels et le placement des sorties cachées.
- Si les chutes d'un niveau à l'autre font des dégâts (jamais mortels).
- L'ordre de construction (on peut faire un village complet avant les deux autres).

## Réponses après relecture

### Décidé par l'utilisateur
- **Rôles (conseil, unité spéciale, police, garde)** : ils n'existent pas encore en base (seuls grade — dont `kage` —, clan et rang staff existent ; unités prévues en V2 du GDD). **En attendant, les lieux fermés « Kage / conseil / unité / police / garde » ne s'ouvrent que pour le Kage du village (grade `kage` + même village) et le staff.** Chaque porte garde sa règle prévue dans les données, pour être branchée sur les vrais rôles plus tard. Pas de nouveau rôle en base dans ce projet.
- **Domaines de clan** : le **staff attribue un domaine libre à un clan validé, en jeu (panel F1)**. Un domaine sans clan : seul le staff entre dans son refuge. Il faut donc, dans ce projet : un identifiant stable par domaine, la liaison domaine → clan en base (migration D1 + route du Worker, droits staff revérifiés et action loggée comme les autres actions staff), et l'action dans F1 (attribuer / retirer). Un membre du clan = perso dont le `clan` est ce clan **et** `clan_validated = 1`, du même village.

### Choix de Claude (modifiables)
- **Porte fermée** : un joueur autorisé l'ouvre (touche d'utilisation), elle se referme seule après quelques secondes. **Suivre quelqu'un de près est toléré** (c'est du RP). C'est une porte partagée : son état ouvert / fermé est décidé et synchronisé par l'hôte, et son collider est actif chez tous quand elle est fermée.
- **Jonction tunnel ↔ pièce fermée** : la porte entre les deux **ne s'ouvre que depuis le côté pièce**. On sort par le tunnel, on n'entre pas par lui. Les sorties cachées restent ouvertes à tous ; elles mènent seulement jusqu'à ces portes.
- **Cellules** : simple salle fermée (règle « police/garde », donc Kage + staff en attendant les rôles). **Pas de vrai système de détention** dans ce projet (enfermer quelqu'un, garder l'état à la déconnexion, interdire les jutsus) : sous-projet plus tard.
- **Shunshin et bond** ne doivent traverser ni une porte fermée, ni un mur ou un plafond de souterrain (à tester).
