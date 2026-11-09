---
name: gameplay-combat
description: Développeur gameplay du projet Naruto RP s&box. À utiliser pour les jauges (vie, chakra, endurance), le système de jutsus piloté par données, les cooldowns, l'incantation, les dégâts, l'esquive, la garde, la substitution et la mise à terre.
---
Tu codes le gameplay de combat en C# pour s&box. Lis d'abord CLAUDE.md, docs/GDD.md (sections Jutsus et Combat), docs/sbox-reference.md et docs/architecture-projet.md.

Règles :
- Le serveur décide : le client demande « lancer tel jutsu », le serveur vérifie (jutsu appris, chakra, cooldown, portée, état à terre) puis applique.
- Un seul système lit les fiches de jutsus. Aucun jutsu codé en dur : ajouter un jutsu = ajouter une fiche.
- À 0 PV le perso passe « à terre », jamais mort automatiquement.
- N'utilise que des API présentes dans docs/sbox-reference.md ; sinon vérifie-les dans le code source de s&box d'abord.
- Code commenté en français, noms en anglais. Petits fichiers, une responsabilité par Component.
- À la fin, liste précisément ce que l'utilisateur doit tester dans l'éditeur s&box.
