# Avancement du projet

Fichier tenu à jour par Claude après chaque tâche. À lire en premier à chaque session.

## Statut actuel

Phase : **MVP — fondations**. Joueur multijoueur, jauges et système de jutsus en place et testés dans l'éditeur (2 joueurs).

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
2. [~] **Architecte** : format des fiches jutsus figé et vérifié (`Code/Jutsus/JutsuDefinition.cs`), points vérifiés notés dans `sbox-reference.md` §11. **Reste** : schéma de données des persos (à faire avec la tâche 7).
3. [x] **Joueur de base** : `GameManager` (lobby + spawn par connexion, `OwnerTransfer.Fixed`), prefab `prefabs/player.prefab`, `SpawnPoint` dans la scène. Testé à 2 joueurs.
4. [x] **Jauges** : `NinjaStats` (vie, chakra avec régén concentration à l'arrêt, endurance qui baisse en courant), à terre à 0 PV, étourdi, ralenti. Tout en `[Sync(FromHost)]`.
5. [x] **Système de jutsus** : `JutsuCaster` (touches 1-9 → `RequestCast` validé par l'hôte), cooldowns, incantation interruptible par un coup, livraisons contact / projectile / zone / soi. 3 fiches de test dans `data/jutsus/test/`. Écart avec l'architecture : logique dans un component par joueur au lieu d'un `CombatSystem` global (plus simple, état local au joueur).
6. [ ] **Combat** : dégâts, esquive, garde, substitution, mise à terre à 0 PV, relève par médic-nin.
7. [ ] **Backend Cloudflare** : Worker + D1 (tables persos, jutsus_appris, sanctions, logs), clé serveur, endpoints de lecture/écriture du perso.
8. [ ] **Persistance** : le serveur charge le perso à la connexion et le sauvegarde (déconnexion + intervalle).
9. [ ] **Création de perso** : UI Razor en 6 étapes du GDD, filtre de noms canon.
10. [~] **HUD** : `Code/UI/Hud.razor` fait en avance (jauges vie/chakra/endurance, barre de jutsus avec coût et cooldown, états à terre/étourdi), vérifié par capture. **Reste** : relecture par le relecteur, style définitif.
11. [ ] **Staff de base** : rangs staff, téléportation, spectateur, kick, logs envoyés au Worker.
12. [ ] **Contenu MVP** : 3 jutsus par nature + médical, taijutsu, kenjutsu (fiches de données).
13. [ ] **Village Konoha** : map ou blocage avec assets existants (voir avec l'utilisateur qui fait la map).

## À tester par l'utilisateur

- Lancer Play dans l'éditeur, appuyer sur **1**, **2**, **3** : le HUD en bas montre les jauges et les cooldowns ; pas encore d'effet visuel des jutsus, mais la console affiche `[Combat] ... lance ...`. Courir (Shift) longtemps : la course doit s'arrêter quand l'endurance est vide.
- Plus tard, avec un ami : vérifier que les jutsus le touchent bien à distance réelle (ping).

## Questions en attente pour l'utilisateur

Voir « Questions ouvertes » dans `docs/GDD.md`.

## Journal

- 2026-11-09 : création du dossier projet, GDD, référence API.
- 2026-11-09 : init Git + `.gitignore` (caches `.sbox/`, `obj/`, `*.csproj`/`*.sln` générés, assets compilés `*_c`, fichiers de secrets).
- 2026-11-09 : projet s&box `nrp` créé par l'utilisateur ; doc + dépôt Git déplacés dans ce dossier. Console : seulement des erreurs moteur sans gravité (son `denyundo.vsnd_c` manquant, renommage du cache de shaders).
- 2026-11-09 : tâches 3, 4, 5 faites et testées en direct via `sbox-cli` (2 joueurs). Deux relectures faites (corrections : zone bloquée par les murs, cooldowns envoyés au client, ralentis qui ne se prolongent pas ; puis : vitesse mesurée sur la position, seuil d'endurance, init dans OnAwake, auteur des dégâts loggé)). Vérifié : l'hôte ne peut pas déplacer un joueur client → blocages de mouvement côté client (limite notée dans le code).
