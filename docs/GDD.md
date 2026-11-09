# GDD – Naruto RP sur s&box

Version du 9 novembre 2026. Version éditable en ligne : https://claude.ai/artifact/N2urAQccJy1DyJ4vJMXCKH

## Vision et principes

Un serveur Naruto RP sur s&box où le jeu fournit les systèmes et où les joueurs écrivent toute l'histoire, avec une progression validée par les staffs comme sur FSC.

- **Repris de l'univers** : les noms de villages (Konoha, Suna, Kumo) et certains jutsus.
- **Pas repris** : aucun personnage canon, aucune trame officielle. Le lore vient du RP des joueurs.
- **Diffusion** : gratuit sur la plateforme s&box, pas de vente ni de sortie Steam à part.
- **Les systèmes servent le RP** : chaque mécanique crée des situations à jouer.
- **Staff d'abord** : chaque système a son outil de contrôle et ses logs dès sa première version.
- **Jutsus en données** : un jutsu = une fiche, pas du code écrit à la main.

## Les 3 villages au lancement

| Village | Pays | Terrain et ambiance | Ce qui le distingue en RP |
| --- | --- | --- | --- |
| Konoha | Feu | Forêt, falaises, grand village dense | Village le plus peuplé, diplomatie et alliances |
| Suna | Vent | Désert, canyons, tempêtes de sable | Isolé, ressources rares, méfiance envers les étrangers |
| Kumo | Foudre | Montagnes, pics, orages | Village militaire, fierté et discipline |

- **Hiérarchie** : Kage (joueur), conseil, chefs d'unité, puis les grades.
- **Unités internes** : garde, unité spéciale, corps médical, police, sur candidature RP.
- **Diplomatie** : neutre au lancement. Alliances et guerres décidées en RP par les Kage, enregistrées par le staff.
- **Nature de chakra** : aucune imposée par village, traditions possibles (raiton à Kumo).
- Les villages sont séparés par des **terres neutres** (rencontres, conflits, récolte).

## Création de personnage

1. **Nom et prénom** libres, filtre qui refuse les noms de personnages canon.
2. **Village** : Konoha, Suna ou Kumo. Changement ensuite par RP + staff.
3. **Apparence** : corps, visage, coiffure, tenue de départ.
4. **Nature de chakra** : une seule au départ, la deuxième plus tard.
5. **Voie** : ninjutsu, taijutsu, kenjutsu, genjutsu ou médical.
6. **Clan** : sans clan, ou clan original validé par le staff.

2 personnages par compte conseillés. On commence élève de l'académie.

## Progression et grades

Pas d'XP : examen RP puis validation staff, puis enregistrement en base.

| Grade | Jutsus | Débloque |
| --- | --- | --- |
| Élève | Rang E | Académie |
| Genin | Rang D | Missions D et C |
| Chûnin | Rangs C et B | 2e nature |
| Jônin | Rang A | Donner des missions B à S |
| Kage | Rang S | Élu en RP parmi les jônin, confirmé par le staff |

Des rangs propres au serveur (comme Kakunin sur FSC) peuvent s'ajouter entre deux grades.

## Jutsus et natures de chakra

- **Natures** : katon, fûton, raiton, doton, suiton.
- **Voies sans nature** : taijutsu, kenjutsu, genjutsu, médical.
- **Rang** : E à S, limité par le grade.
- Apprentissage en RP auprès d'un joueur qui maîtrise le jutsu, puis déblocage par le staff.

| Champ | Rôle | Exemple |
| --- | --- | --- |
| id | Identifiant unique | raiton_decharge |
| nom | Nom affiché | Décharge foudroyante |
| nature / voie | Ce qu'il faut maîtriser | raiton |
| rang | E à S | C |
| coût_chakra | Chakra consommé | 25 |
| cooldown_s | Délai avant réutilisation | 8 |
| incantation_s | Temps des signes, interruptible | 0,6 |
| type | Projectile, zone, contact, buff, soin, déplacement | contact |
| effet | Dégâts, soin, étourdissement, ralentissement | dégâts 30 + étourdi 1 s |
| visuel / son | Effet et son associés | eclair_main / zap_01 |

MVP : 3 jutsus par nature, plus 3 en médical, 3 en taijutsu et 3 en kenjutsu.

## Combat et mort RP

- **Jauges** : vie, chakra, endurance. Recharge du chakra lente, plus rapide en concentration à l'arrêt.
- **Défense** : esquive (endurance), garde, substitution (chakra, cooldown long).
- **Signes** : incantation interruptible par un coup.
- **À terre** : à 0 PV, le perso tombe, peut parler mais pas agir. Un médic-nin peut le relever.
- **Mort RP** : jamais automatique (accord du joueur, guerre déclarée ou décision staff), toujours loggée.
- **Règles HRP** : pas de combat sans RP préalable, pas d'attaque en zone protégée hors événement.

## Économie, missions et ressources

- Monnaie : **ryô**.
- **Missions** D à S : D et C générées par des PNJ ; B à S données en RP par jônin/Kage, validées par le staff.
- **Ressources** : minerais, plantes médicinales, bois, dans les terres neutres, points qui réapparaissent.
- **Usages** : vente, fabrication d'armes et parchemins, ingrédients médicaux.
- **Boutiques** : une par village, prix fixés par le staff.
- La carte web interactive pourra afficher les zones de ressources depuis les mêmes données.

## Staff et administration

| Outil | Où | Rôle |
| --- | --- | --- |
| Validation des grades et jutsus | Jeu + web | Débloquer après examen ou apprentissage RP |
| Téléportation, invisibilité, spectateur | Jeu | Surveiller une scène sans la casser |
| Sanctions (avertissement, kick, ban temporaire) | Jeu + web | Motif obligatoire, historique |
| Logs | Web | Combats, mises à terre, morts RP, transferts d'argent, déblocages |
| Gestion des villages | Web | Kage, unités, diplomatie, boutiques |
| Whitelist (optionnelle) | Web | Accès sur candidature |

Rangs de staff : modérateur, staff, admin.

## Technique

- **Client s&box** : affichage, UI Razor, entrées.
- **Serveur s&box** : combat, jutsus, synchro réseau, décide de tout.
- **Worker Cloudflare** : API sécurisée (clé serveur), règles de validation.
- **Base D1** : persos et grades, jutsus appris, sanctions et logs.
- **Fiches jutsus** : données lues au démarrage par le serveur.
- **Panel web staff** et **carte interactive** : passent par le même Worker.
- Code dans un repo GitHub, une branche par sous-agent, revue avant fusion.
- Assets : d'abord ceux de la plateforme s&box, puis Higgsfield (plan payant requis) + Blender.

## Roadmap

1. **MVP** : un village jouable (Konoha), création de perso, déplacement, 3 jutsus par nature, combat et mise à terre, sauvegarde du perso, panel staff de base.
2. **V1** : Suna et Kumo, terres neutres, grades et validation staff, missions D et C, ressources, panel web.
3. **V2** : missions B à S, fabrication, unités de village, diplomatie, clans originaux, carte web reliée.

## Questions ouvertes

- Nom du serveur ?
- Grades exacts : ceux de FSC (avec Kakunin) ou échelle simple ?
- Clans canon autorisés, ou seulement originaux ?
- Whitelist à l'entrée ou serveur ouvert ?
- Liste des jutsus repris de l'univers pour le MVP ?
- Nombre de joueurs visés au lancement ?
- Qui fait la map ?
