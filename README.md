# Naruto RP sur s&box

Serveur de jeu de rôle ninja sur s&box, inspiré de FSC Naruto RP et Zenkai RP.

## Démarrer avec Claude Code

1. Dézippe ce dossier où tu veux (par exemple `Documents/sbox-naruto-rp`).
2. Ouvre un terminal dans ce dossier et lance `claude`.
3. Colle cette phrase :

> Lis CLAUDE.md et docs/PROGRESS.md, puis continue le projet en attaquant la prochaine tâche avec les sous-agents adaptés, et dis-moi à la fin ce que je dois tester dans s&box.

C'est la même phrase à chaque session : `docs/PROGRESS.md` garde la trace de l'avancement.

## Contenu

- `CLAUDE.md` : règles et décisions du projet, lues automatiquement par Claude Code.
- `docs/GDD.md` : design du jeu.
- `docs/PROGRESS.md` : avancement et prochaines tâches.
- `docs/sbox-reference.md`, `docs/pieges-gmod-vs-sbox.md`, `docs/architecture-projet.md` : référence technique s&box.
- `.claude/agents/` : les 6 sous-agents (architecte, gameplay-combat, reseau-persistance, ui-razor, systemes-rp, relecteur).
