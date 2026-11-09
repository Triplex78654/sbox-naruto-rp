# Avancement du projet

Fichier tenu à jour par Claude après chaque tâche. À lire en premier à chaque session.

## Statut actuel

Phase : **MVP — fondations**. Aucune ligne de code de jeu pour l'instant.

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

1. [ ] **Setup** : initialiser le repo Git (si pas déjà fait), `.gitignore` adapté à s&box, créer le projet s&box (l'utilisateur le crée depuis l'éditeur s&box si besoin, puis on ajoute nos dossiers selon `docs/architecture-projet.md`).
2. [ ] **Architecte** : vérifier les points « NON VÉRIFIÉ » de la référence, figer le format des fiches jutsus et le schéma de données des persos.
3. [ ] **Joueur de base** : spawn, déplacement, caméra, en multijoueur (test : 2 instances / un ami).
4. [ ] **Jauges** : vie, chakra, endurance, synchronisées, côté serveur.
5. [ ] **Système de jutsus** : chargement des fiches, lancement (intention client → validation serveur), cooldowns, incantation interruptible. 1 jutsu de test par type (contact, projectile, zone).
6. [ ] **Combat** : dégâts, esquive, garde, substitution, mise à terre à 0 PV, relève par médic-nin.
7. [ ] **Backend Cloudflare** : Worker + D1 (tables persos, jutsus_appris, sanctions, logs), clé serveur, endpoints de lecture/écriture du perso.
8. [ ] **Persistance** : le serveur charge le perso à la connexion et le sauvegarde (déconnexion + intervalle).
9. [ ] **Création de perso** : UI Razor en 6 étapes du GDD, filtre de noms canon.
10. [ ] **HUD** : jauges, barre de jutsus, cooldowns.
11. [ ] **Staff de base** : rangs staff, téléportation, spectateur, kick, logs envoyés au Worker.
12. [ ] **Contenu MVP** : 3 jutsus par nature + médical, taijutsu, kenjutsu (fiches de données).
13. [ ] **Village Konoha** : map ou blocage avec assets existants (voir avec l'utilisateur qui fait la map).

## À tester par l'utilisateur

(rien pour l'instant)

## Questions en attente pour l'utilisateur

Voir « Questions ouvertes » dans `docs/GDD.md`.

## Journal

- 2026-11-09 : création du dossier projet, GDD, référence API.
