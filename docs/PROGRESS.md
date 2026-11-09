# Avancement du projet

Fichier tenu à jour par Claude après chaque tâche. À lire en premier à chaque session.

## Statut actuel

Phase : **MVP terminé (version de test)**. Reste à faire tester à plusieurs, mettre le backend en ligne, et remplacer les volumes / effets provisoires par de vrais assets.

Ancienne phase : MVP — fondations. Joueur multijoueur, jauges et système de jutsus en place et testés dans l'éditeur (2 joueurs).

**Claude pilote l'éditeur** via `sbox-cli` (voir `docs/sbox-reference.md` §11) : compilation, mode Play, 2e client local, tests C# en direct. Garder l'éditeur ouvert sur le projet pendant les sessions.

Dossier du projet : `C:\Users\Estam\Documents\sbox-naruto-rp\nrp` (le `.sbproj` est à côté de `CLAUDE.md`).

## Fait

- [x] GDD rédigé (`docs/GDD.md`)
- [x] Instructions projet (`CLAUDE.md`) et sous-agents (`.claude/agents/`)
- [x] Référence de l'API s&box tirée du code source (`docs/sbox-reference.md`, `docs/pieges-gmod-vs-sbox.md`, `docs/architecture-projet.md`) — relire les points marqués « NON VÉRIFIÉ »

## Points clés trouvés dans le code de s&box (à respecter)

- Stats de jeu (PV, chakra, grade…) en `[Sync( SyncFlags.FromHost )]`, jamais en `[Sync]` simple (sinon le client les écrit).
- Le code de jeu ne lit ni les variables d'environnement ni `System.IO.File` : la clé du Worker doit venir de `FileSystem.Data` ou d'un fichier `*.Server.cs` ignoré par Git. **À tester tôt** (tâche 7).
- `*.Server.cs` et `#if SERVER` ne sont compilés que sur un serveur dédié : prévoir un faux client API en mémoire pour tester dans l'éditeur.
- HTTP : passer par `Http.*` (pas `HttpClient`), pas d'IP directe ou privée.
- Côté serveur dédié : `Application.IsDedicatedServer` (`Networking.IsHost` est vrai hors réseau aussi).
- Jutsus en GameResource (extension `.jutsu`), chargés avec `ResourceLibrary.GetAll<T>()`.

## Prochaines tâches (dans l'ordre)

1. [x] **Setup** : repo Git, `.gitignore` (template s&box + secrets), projet s&box `nrp` (template player controller, ident `local.nrp`). Se charge et tourne dans l'éditeur.
2. [x] **Architecte** : format des fiches jutsus (`Code/Jutsus/JutsuDefinition.cs`) et schéma des persos (`backend/migrations/0001_init.sql`) figés et vérifiés. Points vérifiés dans `sbox-reference.md` §11.
3. [x] **Joueur de base** : `GameManager` (lobby + spawn par connexion, `OwnerTransfer.Fixed`), prefab `prefabs/player.prefab`, `SpawnPoint` dans la scène. Testé à 2 joueurs.
4. [x] **Jauges** : `NinjaStats` (vie, chakra avec régén concentration à l'arrêt, endurance qui baisse en courant), à terre à 0 PV, étourdi, ralenti. Tout en `[Sync(FromHost)]`.
5. [x] **Système de jutsus** : `JutsuCaster` (touches 1-9 → `RequestCast` validé par l'hôte), cooldowns, incantation interruptible par un coup, livraisons contact / projectile / zone / soi. 3 fiches de test dans `data/jutsus/test/`. Écart avec l'architecture : logique dans un component par joueur au lieu d'un `CombatSystem` global (plus simple, état local au joueur).
6. [x] **Combat** : `NinjaDefense` (garde clic droit : -60 % dégâts contre de l'endurance ; esquive X : invulnérable 0,35 s, 25 endurance ; substitution Z : 20 chakra, cooldown 30 s, le prochain coup dans la seconde est évité + recul), `ReviveAction` (médic-nin maintient E sur un ninja à terre 4 s, annulé par un coup ou l'éloignement, relève à 25 % PV). Choix validé par l'utilisateur : **la garde ignore étourdissement et ralenti**. Testé à 2 joueurs, relu. `IsMedic` réglé dans le prefab en attendant les données du perso.
7. [~] **Backend Cloudflare** : fait **en local** dans `backend/` (Worker TypeScript + D1 : persos, jutsus appris, historique des grades, sanctions, logs, staff). Clé serveur en Bearer, droits staff revérifiés, 15 tests de routes OK (`npm test`), relu. **Reste** : mise en ligne quand l'utilisateur aura un compte Cloudflare (étapes dans `backend/README.md`).
8. [x] **Persistance** : `IApiClient` (+ `WorkerApiClient`, `MemoryApiClient`, `ApiConfig`). À la connexion : chargement du perso (nom, village, grade, médic, ryô, jutsus appris → barre de jutsus), joueur banni refusé, API indisponible → refusé (jamais de partie sans données). Sauvegarde toutes les 2 min et à la déconnexion. Logs `downed` / `revived`. Testé de bout en bout dans l'éditeur contre le Worker local (port 8080), relu (corrections : sauvegardes dans l'ordre, sauvegarde de tous à l'arrêt, pas de double objet pour un même joueur, serveur dédié sans config refuse les connexions).
9. [x] **Création de perso** : à la connexion, écran de choix (2 emplacements) puis assistant en 6 étapes (nom, village, apparence, nature, voie, clan). Filtre canon côté UI, hôte et Worker. Choix et création par RPC statiques validés par l'hôte (`GameManager.Characters.cs`). Testé dans l'éditeur et contre le Worker local. **Apparence** : avatar s&box pour l'instant (étape affichée comme « plus tard »). Relu ; corrigé : contournement du filtre (lettres cyrilliques, « N'aruto »), joueur bloqué si le rechargement échoue après création. Clan original : colonne `clan_validated` (migration 0002), **validation par le staff à faire avec la tâche 11**.
10. [x] **HUD** : bloc identité + jauges libellées + touches de défense (nom réel de la touche selon le clavier), barre de jutsus (bande de couleur de la nature, rang, coût en rouge si chakra insuffisant, cooldown), barre d'incantation, relève, messages staff. Les menus (perso, staff) ne capturent plus la souris ni n'assombrissent l'écran une fois fermés.
11. [x] **Staff de base** : panel **F1** (`StaffPanel.razor`) réservé aux rangs modérateur / staff / admin (chargés depuis la base). Aller vers / ramener, suivre en spectateur, invisibilité (intouchable, ne peut pas combattre), avertir / kick / ban temporaire (motif obligatoire), grade, déblocage de jutsu (contrôle grade + nature + voie), médic-nin, validation ou refus de clan. Tout passe par des RPC validées par l'hôte (`StaffCommands.cs`), les actions en base sont revérifiées par le Worker et loggées. Testé à 2 joueurs. Relu ; corrigé : hiérarchie des sanctions (pas sur soi ni sur un rang égal ou supérieur, aussi côté Worker), staff invisible traversé par les attaques et sans relève, anti double clic. Limite : un rang retiré en base reste actif jusqu'à la reconnexion du staff.
12. [x] **Contenu MVP** : 24 fiches dans `Assets/data/jutsus/<voie>/` (3 par nature + médical, taijutsu, kenjutsu, rangs E à B). Effets visuels construits en code (`Combat/JutsuFx.cs` : lueur d'incantation, boule de projectile, éclat d'impact / de zone à la couleur de la nature) + sons du moteur. Fiches de test supprimées. Règle de publication `data/jutsus/*`. Relu et rééquilibré (coups E : cooldown 2,5 s ; un coup de moins de 10 dégâts n'interrompt plus les signes).
13. [x] **Village Konoha** (blocage) — remplacé par le **monde** (voir 14).

14. [~] **Monde (une seule carte, ~4 km)** : plan « trèfle ». Fait : **pétale de Konoha** (village ~450 m inspiré de l'anime : enceinte ronde, grande porte face au centre, poste de garde, rue commerçante + échoppe de ramen, place, tour du Hokage, falaise des Kage avec 5 emplacements de visages vides pour les Kage élus, académie, hôpital, rivière, terrains d'entraînement, arène des examens, quartier des clans originaux, parc, quartiers résidentiels), **zone du Pays du Feu** (forêt, 3 clairières de missions), **frontière** (palissade + bornes + poste-frontière sur la route), **terres neutres de Konoha** (rochers, ruines, points de ressources), **centre commun** (temple en ruine, pierres levées, marché neutre, arène neutre, minerais rares, routes vers Suna et Kumo). Script : `docs/monde-blocage.cs.txt`. Testé : apparition sur la place, 54 i/s, test de taille 4 km OK. **Reste** : pétales de Suna (désert) et Kumo (montagnes), relief (composant Terrain), déplacements ninja.

## À tester par l'utilisateur

- **Redémarre l'éditeur avant de tester** : il garde en mémoire d'anciennes valeurs de fiches et les fiches de test supprimées (limite de l'éditeur, voir référence §11).
- Lancer Play (scène Konoha) : l'écran **Choisis ton personnage** s'affiche. Créer un perso (essayer un nom canon pour voir le refus), puis jouer. Avec l'API en mémoire, un perso créé reçoit les 3 jutsus de test et le statut médic (les vrais persos commencent élèves, sans jutsu).
- En jeu, appuyer sur **1**, **2**, **3** : le HUD en bas montre les jauges et les cooldowns ; pas encore d'effet visuel des jutsus, mais la console affiche `[Combat] ... lance ...`. Courir (Shift) longtemps : la course doit s'arrêter quand l'endurance est vide.
- Touches de défense : **clic droit** maintenu (garde), **X** (esquive dans la direction ZQSD), **Z** (substitution). Le HUD affiche Garde / Substitution prête / cooldown. Pour la relève il faut 2 joueurs : un à terre, l'autre maintient **E** en le regardant.
- Plus tard, avec un ami : vérifier que les jutsus le touchent bien à distance réelle (ping).

- **Tester avec la vraie base en local** (optionnel) : dans `backend/`, `npx wrangler dev --port 8080 --persist-to .wrangler/editor-state --var SERVER_KEY:cle-editeur-locale`, puis créer `api_config.json` dans `D:\SteamLibrary\steamapps\common\sbox\data\local\nrp#local` avec `{"BaseUrl":"http://localhost:8080","ServerKey":"cle-editeur-locale"}`. **Le supprimer après**, sinon le jeu refuse de te faire apparaître quand le Worker est arrêté. Sans ce fichier, le jeu utilise un perso de test en mémoire.

- **Panel staff** : dans l'éditeur (API en mémoire), tu es admin. Appuie sur **F1** en jeu. Pour un vrai serveur, ajouter les staffs en base (`backend/README.md`).

## Questions en attente pour l'utilisateur

- Mise en ligne du backend : quand l'utilisateur veut, avec son compte Cloudflare gratuit (`backend/README.md`).
Voir « Questions ouvertes » dans `docs/GDD.md`.

## Journal

- 2026-11-09 : création du dossier projet, GDD, référence API.
- 2026-11-09 : init Git + `.gitignore` (caches `.sbox/`, `obj/`, `*.csproj`/`*.sln` générés, assets compilés `*_c`, fichiers de secrets).
- 2026-11-09 : projet s&box `nrp` créé par l'utilisateur ; doc + dépôt Git déplacés dans ce dossier. Console : seulement des erreurs moteur sans gravité (son `denyundo.vsnd_c` manquant, renommage du cache de shaders).
- 2026-11-09 : tâches 3, 4, 5 faites et testées en direct via `sbox-cli` (2 joueurs). Deux relectures faites (corrections : zone bloquée par les murs, cooldowns envoyés au client, ralentis qui ne se prolongent pas ; puis : vitesse mesurée sur la position, seuil d'endurance, init dans OnAwake, auteur des dégâts loggé)). Vérifié : l'hôte ne peut pas déplacer un joueur client → blocages de mouvement côté client (limite notée dans le code).
- 2026-11-09 : tâche 6 (garde, esquive, substitution, relève médic-nin) faite et testée à 2 joueurs. Nouvelles touches X / Z dans `Input.config`.
- 2026-11-09 : tâche 7 en local (Worker + D1 + tests). Le dossier `backend/` n'est pas publié avec le jeu (vérifié). Prochaine : tâche 8, brancher le serveur de jeu sur l'API (`IApiClient` + client mémoire pour l'éditeur).
- 2026-11-09 : tâche 8 faite. Vérifié : dans l'éditeur, HTTP vers localhost seulement sur 80/443/8080/8443.
- 2026-11-09 : tâche 9 (choix + création de perso) faite.
- 2026-11-09 : tâche 11 (staff de base) faite. Nouvelle route Worker `POST /characters/:id/clan`. Touche F1 ajoutée à `Input.config`.
- 2026-11-09 : tâches 10, 12, 13 faites. Toute la roadmap MVP est cochée. Prochaines pistes (V1 du GDD) : zones protégées du village, Suna et Kumo, 2e nature au chûnin, missions, ryô, apparence du perso, vrais modèles et effets.
- 2026-11-09 : **clic gauche = attaque de base** (fiche `data/jutsus/base/base_coup_de_poing.jutsu` : 5 dégâts, 0 chakra, pas de cooldown ; limite serveur anti-autoclic de 4 coups/s). Plus tard : l'arme équipée fournira sa fiche (petit coup d'épée) à la place (`JutsuCaster.BasicAttack`). Pas encore testé en jeu ni relu.
- 2026-11-09 : monde v1 (pétale de Konoha + centre commun). Décision avec l'utilisateur : une seule carte (serveur unique tant que < 64 joueurs), terres neutres qui se rejoignent au centre. Connexion auto au serveur (QueryLobbies + Connect) prévue après le test du serveur dédié.
- 2026-11-09 : **déplacements ninja** codés (compile, PAS encore testés en jeu ni relus) : marche par défaut, Shift = course, double Shift maintenu = course ninja (×1,7, 22 endurance/s mesurés par l'hôte), Espace en course ninja = bond (5 chakra, recharge 1 s, validé par l'hôte), jutsu `shunshin` (type Movement, 15 m, s'arrête avant les murs, à débloquer par le staff). Composant `Player/NinjaMovement.cs` ajouté au prefab. **Prochaine session : tester ces déplacements, faire relire, puis pétales de Suna et Kumo.**
