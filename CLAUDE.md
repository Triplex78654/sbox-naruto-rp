# Naruto RP sur s&box — instructions projet

Tu travailles sur un serveur de jeu de rôle « Naruto RP » (dans l'esprit de FSC Naruto RP ou Zenkai RP sur Garry's Mod), refait sur **s&box** (Facepunch, Source 2, C#, UI Razor, Components + Scenes, sorti le 28 avril 2026).
Réponds et commente le code **en français**. Noms de classes, méthodes et fichiers en anglais.

## À lire au début de chaque session

1. `docs/PROGRESS.md` : où on en est, ce qui est fait, la prochaine tâche. **C'est la source de vérité.**
2. `docs/GDD.md` : le design du jeu (règles, systèmes, roadmap).
3. `docs/sbox-reference.md` et `docs/pieges-gmod-vs-sbox.md` : l'API s&box vérifiée dans le code source. **Ne jamais utiliser une API qui n'y figure pas sans l'avoir vérifiée** dans le code de s&box (dépôt public Facepunch) ou la doc officielle. Si tu vérifies quelque chose de nouveau, ajoute-le à la référence.
4. `docs/architecture-projet.md` : arborescence et conventions de code.

## Décisions déjà prises (ne pas remettre en question sans demander)

- RP libre écrit par les joueurs. **Aucun personnage canon, aucune histoire officielle.** On reprend les noms de villages (Konoha, Suna, Kumo) et certains jutsus.
- Diffusé gratuitement sur la plateforme s&box. Pas de vente, pas de sortie Steam à part.
- 3 villages au lancement : Konoha, Suna, Kumo.
- Progression **comme sur FSC** : pas d'XP, grades obtenus par examen RP puis validés par le staff.
- Le **serveur fait autorité** sur tout. Le client envoie des intentions, le serveur vérifie et applique.
- Les **jutsus sont des données** (une fiche par jutsu) lues par un seul système de combat.
- Persistance des persos : **Cloudflare Worker + base D1**, appelée uniquement par le serveur de jeu avec une clé secrète. Les joueurs n'accèdent jamais à la base.
- Mort RP jamais automatique : à 0 PV le perso tombe à terre.

## Méthode de travail

- Travaille par petites tâches tirées de `docs/PROGRESS.md`, dans l'ordre de la roadmap (MVP d'abord).
- Utilise les sous-agents de `.claude/agents/` : `architecte`, `gameplay-combat`, `reseau-persistance`, `ui-razor`, `systemes-rp`, `relecteur`. Maximum 3 à 4 en parallèle, chacun sur des fichiers différents.
- **Chaque morceau de code passe par le `relecteur`** avant d'être considéré comme fini.
- Tu ne peux pas lancer l'éditeur s&box toi-même s'il n'est pas accessible : quand une étape demande un test en jeu, dis exactement à l'utilisateur quoi tester et quoi te renvoyer (erreurs de la console de l'éditeur, comportement observé).
- Après chaque tâche : mets à jour `docs/PROGRESS.md` (fait / en cours / prochaine tâche / points à tester par l'utilisateur), puis fais un commit Git clair.
- Ne dépense jamais d'argent (crédits, abonnements) sans demander.
- Pas de secrets dans le repo : clés API dans des variables d'environnement ou des fichiers ignorés par Git.

## L'utilisateur

Étudiant en BTS CIEL, il joue sur des serveurs Naruto RP et connaît bien le fonctionnement RP (grades, staff, whitelist). Il teste dans l'éditeur s&box sur son PC (Windows). Explications claires et courtes, sans jargon inutile.
