# Territoires en arcs, biomes et relief de fond — conception

Date : 2026-11-10. Statut : relu, validé par l'utilisateur (2026-11-10).
Repères visuels : `.superpowers/brainstorm/993-1794282681/content/` (`carte-actuelle.html`, `editeur-zones-v2.html`, `frontieres-arcs.html`, choix **B**).
Suite de : `2026-11-10-carte-coins-relief-souterrains-design.md` (tout ce qui y est décidé reste valable sauf ce qui est changé ici).

## Pourquoi

Vue du dessus de la carte actuelle : villages de 500 m dans les coins, pays de 900 m qui **ne se touchent pas** (~700 m de plaine plate et vide entre eux), relief seulement dans les villages (3 marches). L'utilisateur trouve la carte vide : on marche longtemps dans du plat sans savoir où on est. Il veut :

- des **villages plus grands** et plus travaillés, avec **plus de relief** (collines…) ;
- la zone devant la frontière de chaque village **agrandie**, jusqu'à ce que les **territoires se touchent**, avec des **limites** et un **changement de biome** ;
- une **carte de base propre** d'abord : « on va ajouter les éléments après ».

## Décidé par l'utilisateur

1. Les territoires se touchent.
2. Frontières **en arc de cercle**, pas ondulées : chaque pays est un grand **quart de cercle centré sur son coin**, comme les villages.
3. Option **B** : rayon des pays **~1 500 m**. Deux pays voisins se chevauchent ; leur frontière commune est la **ligne droite** du milieu de la carte (x = 0 ou y = 0) là où leurs cercles se croisent. Chaque pays garde un **arc face au centre**. Le **centre commun** est ce qui reste au milieu (losange aux côtés arrondis), neutre.
4. Changement de biome au passage d'une frontière.
5. Carte de base propre maintenant ; les « éléments » viennent après (voir Non-objectifs).
6. Inchangé depuis le projet précédent : 4e coin (bas-gauche) réservé, contenu décidé plus tard ; centre commun gardé.

## Proposé par Claude (l'utilisateur peut changer)

### Carte
- Taille de carte inchangée (carré de 2 800 m, zone jouable ±1 250 m, bande de bord infranchissable de 150 m). Les 4 pays + le centre couvrent **toute** la zone jouable : plus de « terres neutres » vides.
- Pays : Konoha (haut-gauche), Suna (haut-droite), Kumo (bas-droite), 4e coin (bas-gauche) — le 4e coin a lui aussi son quart de cercle de 1 500 m, biome **neutre / à définir** (sol neutre, relief doux).
- Rayon du pays et forme exacte des frontières : quart de cercle de 1 500 m depuis le coin, coupé par les lignes x = 0 et y = 0. Les frontières ne bloquent rien : elles se lisent par le changement de biome et un poste-frontière sur chaque route.

### Biomes
- Konoha : **forêt / prairie** (sol vert). Suna : **désert** (sol sable). Kumo : **montagne / roche** (sol gris-brun). 4e coin et centre : **neutre** (sol terre / herbe rase).
- **Bande de transition** d'environ **60 à 100 m** de part et d'autre de chaque frontière, où le sol passe d'un biome à l'autre en fondu (pas de coupure nette au mètre près).
- Aujourd'hui les couleurs des 3 matériaux du terrain (`Assets/terrain/herbe|roche|sable.tmat`) **ne s'affichent pas** en jeu (cause non trouvée, `docs/sbox-reference.md`). **Décision de l'utilisateur : on construit d'abord tout le reste (territoires, relief, villages), les couleurs du sol se règlent après**, dans une étape à part à la fin. Les zones de biome (quel sol où, largeur du fondu) sont quand même calculées et écrites dans la carte de contrôle du terrain dès maintenant, pour qu'il n'y ait plus qu'à faire marcher l'affichage.

### Relief de fond
- Plus de grande plaine plate : un **relief doux généré** partout hors villages et centre, différent par biome :
  - Konoha : collines arrondies, vallons.
  - Suna : dunes allongées, quelques plateaux à bords raides.
  - Kumo : relief le plus marqué, grosses bosses rocheuses qui montent vers le coin.
  - 4e coin : ondulations faibles.
  - Centre commun : presque plat (lieu de rencontre, marché, arène).
- **Tout reste marchable** hors villages et bord de carte (pentes < ~35°) : le relief de fond donne du paysage, pas des murs. Les routes porte → centre restent praticables (aplanies sur leur tracé). **Exception (ajout « verticalité », plus bas)** : les flancs des grandes montagnes, déclarés un par un dans `FLANCS_RAIDES` (`tools/relief.py`), peuvent dépasser 35° ; chacune a un chemin en lacets marchable.
- Le relief s'adoucit en approchant des remparts des villages et du centre, pour ne pas casser leurs niveaux.

### Villages
- Rayon du rempart : **500 → ~700 m** (quart de cercle dans le coin, comme avant).
- Ma proposition : **agrandir l'existant à l'échelle** (plans v3 × ~1,4 sur les positions : niveaux, rampes, rues, emplacement des bâtiments, souterrains, sorties cachées, portes, domaines de clan). Les **bâtiments gardent leur taille** : les espaces entre eux grandissent et serviront aux éléments ajoutés plus tard.
- **Plus de relief dans les villages** : en plus des 3 niveaux, des buttes et creux doux à l'intérieur de chaque niveau (sans toucher aux sols des bâtiments, places, rues et rampes, qui restent plats).
- Tout ce qui marche aujourd'hui doit encore marcher : apparition par village, portes fermées, domaines de clan (mêmes identifiants), souterrains avec plafonds, sorties cachées, bord infranchissable.

### Contenu déjà posé hors villages
La forêt et les 3 clairières du Pays du Feu, le poste-frontière, les ruines, les points de ressources, le temple, le marché, les arènes, les routes : **gardés et replacés** dans la nouvelle géométrie (dans leur pays / au centre), posés sur le nouveau relief (rien qui flotte ou qui s'enfonce).

## Contraintes

- Chaîne actuelle à garder : `tools/plans-villages-v3.py` → `tools/plan-vers-blocage.py` → `tools/relief.py` → éditeur (`tools/terrain-import.cs.txt` puis `docs/monde-blocage-v3.cs.txt`) → sauvegarde par le contournement §11. Constantes de carte dans `tools/carte.py`.
- Le relief (heightfield) **ne bloque rien par en dessous** : tout souterrain déplacé garde son plafond (« toit du puits »), et les tests `tools/tests/*.cs` doivent tous repasser.
- Objets posés au sol : leur hauteur doit suivre le nouveau relief (`height_at`).
- Règle projet : toute API s&box non listée dans `docs/sbox-reference.md` est vérifiée dans le code source de s&box avant usage, puis ajoutée à la référence.
- Performances : rester ≥ 55 i/s minimum dans chaque village (aujourd'hui ≥ 210).

## Hors de ce projet (non-objectifs)

- Les éléments placés à la main plus tard : marais, lacs, collines précises, nouveaux bâtiments, zones de missions de Suna / Kumo. (Ponts, rivières / ravins internes, végétation, dunes, arches et cascades **font partie** de ce projet, voir les ajouts.)
- Le contenu du 4e coin.
- Vrais modèles, textures détaillées, végétation, eau.

## À éviter absolument (anti-objectifs)

- Des frontières **ondulées** ou en forme libre (l'utilisateur veut des arcs).
- Un biome qui **ne se voit pas** en jeu **à la fin du projet** (le sol gris est toléré pendant la construction, pas après l'étape des couleurs).
- Un relief de fond qui fait **des murs** ou coupe les routes.
- Casser ce qui marche : joueur coincé à l'apparition, souterrain par lequel on remonte à travers le sol, porte contournable, sortie de carte possible.

## Ajout de l'utilisateur à la validation : relief, ponts, cohérence

Demande : « faire des reliefs, des ponts qui lient des zones entre elles, que tout soit cohérent ». Puis, sur les frontières : **pas de ravin de frontière** (Konoha et Kumo ne se touchent pas ; les frontières de Suna : « ravin vide ou rien, un chemin classique ») — « fais juste un bel environnement de chaque zone ».

- **Frontières** : pas d'obstacle. On passe d'un pays à l'autre à pied n'importe où ; la frontière se lit par le **changement de biome** (bande de transition) et par un petit **poste-frontière / borne** sur chaque route qui la traverse.
- **Ponts** : là où le relief **interne** de chaque pays les justifie — sur les rivières et ravins de Konoha (avec cascades), entre les pics de Kumo, au-dessus des creux entre plateaux de Suna. Chaque route qui croise une rivière ou un ravin a son pont.
- **Cohérence** : les routes restent en pente douce ; aucune route coupée sans pont ; on peut toujours aller à pied de n'importe quel village à n'importe quel autre et au centre (test automatique de chemin à prévoir) ; tout creux (ravin, lit de rivière) a au moins une pente marchable pour en ressortir ; l'eau suit la pente (rivières qui descendent, cascades aux marches du relief, bassins en bas).

## Ajout de l'utilisateur : ambiance des biomes

Demande : « des cascades aussi, ça pourrait être joli ; un monde rempli de végétation pour Konoha, légèrement pour Kumo, et un désert pour Suna avec des dunes, des arches ».

- **Konoha** : **végétation dense** partout dans le pays (forêts serrées, bosquets, haies), clairières et routes dégagées. Village plus arboré aussi (parcs, bords de rues), sans gêner les bâtiments ni les passages.
- **Kumo** : **végétation légère** (arbres isolés, petits bosquets sur les pentes), surtout de la roche.
- **Suna** : **désert** sans arbres (sauf oasis plus tard), **dunes** dans le relief de fond, **arches de roche** naturelles (3 à 6 dans le pays, assez grandes pour passer dessous, certaines sur une route ou un pont naturel).
- **Cascades** : là où le relief fait une marche raide avec de l'eau (rivières et ravins de Konoha, falaises de Konoha et de Kumo, falaise des Kage possible), 4 à 8 cascades sur la carte. En blocage : chute d'eau = forme bleue translucide sans collision + bassin en bas ; un vrai effet d'eau viendra avec les vrais assets.
- Tout est en **blocage** (formes simples : arbres = tronc + boule/cône, arches = blocs de roche) et se pose **sur** le relief (hauteur via `height_at`). Performances : la végétation dense de Konoha est le plus gros risque — rester ≥ 55 i/s (instances regroupées, distance d'affichage si besoin).
- Ces éléments sortent donc des non-objectifs : ils font partie de ce projet.

## Réponses après relecture (choix de Claude, modifiables)

- **Postes-frontière** : petit blocage au bord de chaque route qui traverse une frontière, jamais sur la chaussée ni sur un point d'apparition.
- **Fondu des biomes** : un fondu doux est l'objectif ; si la méthode de coloration qui marche ne le permet pas, une transition **en damier / par paliers** sur la même largeur est acceptée.
- **Tests `tools/tests/*.cs`** : leurs valeurs codées en dur (coordonnées, hauteurs, distances) peuvent être **recalées** sur la nouvelle géométrie tant que ce qu'ils vérifient reste le même ; ne jamais affaiblir une vérification pour la faire passer.

## Laissé au constructeur

- Les tailles exactes (rayon 1 500 m, bande de transition, hauteur des collines, facteur d'échelle des villages), tant que l'esprit est gardé.
- La technique du bruit de relief et du fondu des sols.
- Le placement exact des contenus replacés.
- L'ordre de construction.

## Ajout de l'utilisateur : verticalité (2026-11-10)

Demande : « plus de grosses montagnes à des moments, de vraies belles et grandes cascades, des dunes, qu'il y ait vraiment une verticalité ».

- **Konoha** : un grand **massif boisé de ~150 m** au fond du pays (loin du village et de la route, côté Suna / bord nord), une **grande cascade de 40 à 60 m** qui tombe de sa falaise dans un **lac**, et ce lac est la **source de la rivière** : massif → lac → rivière → pont de la route → étang en aval (l'ancien torrent sans source est remplacé par cette rivière unique).
- **Suna** : de **vraies dunes de 30 à 60 m** (en plus des petites rides), et une grande **mesa** rocheuse (plateau à bords verticaux) coupée par un **canyon**, avec un pont d'une moitié du plateau à l'autre.
- **Kumo** : la **plus haute montagne de la carte** (pic de ~290 m, flancs en falaise) avec une **grande cascade** de plus de 40 m qui tombe dans un bassin. Les deux petits pics et leur pont sont gardés.
- **Règle d'escalade (décidée)** : chaque grande montagne / massif / mesa a **un seul chemin en lacets** marchable (≤ 30° le long, ≥ 4 m de large) du pied au sommet ; le reste des flancs peut dépasser 35° (on y monte par bond / Shunshin, voulu). Les dunes restent marchables.
- Inchangé : villages, centre plat, routes ≤ 10° avec ponts, bord infranchissable, sorties cachées (relief tenu à l'écart), 4e coin doux.
