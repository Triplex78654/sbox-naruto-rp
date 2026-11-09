# Pièges : Garry's Mod / ancien s&box → s&box actuel

> Version : 9 novembre 2026. Sources : code public `Facepunch/sbox-public` (commit `987ad085`) et doc `sbox.game/dev/doc`.
> Le détail des API correctes est dans `sbox-reference.md`.
>
> **Règle d'or :** si un exemple trouvé sur Internet contient `Entity`, `[Net]`, `ClientRpc`, `Game.IsServer`, `Simulate(`, `hook.Add` ou du Lua, il **ne s'applique pas** au s&box actuel.

## 1. Garry's Mod (GLua) → n'existe PAS dans s&box

| GMod / GLua | À la place dans s&box | Note |
|---|---|---|
| Lua, fichiers `.lua`, `include()`, `AddCSLuaFile()` | C# dans `Code/`, compilé automatiquement | Pas de Lua du tout. |
| `if SERVER then` / `if CLIENT then` | `Networking.IsHost`, `IsProxy`, `#if SERVER` ou fichiers `*.Server.cs` | `#if SERVER` n'est défini **que sur serveur dédié**. En éditeur/lobby P2P, l'hôte est un joueur. |
| `net.Start / net.Send / net.Receive`, `util.AddNetworkString` | RPC : `[Rpc.Host]`, `[Rpc.Broadcast]`, `[Rpc.Owner]` | Pas de chaînes réseau à déclarer. |
| `SetNWInt`, `SetNW2Var`, `NetworkVar` | `[Sync]` / `[Sync( SyncFlags.FromHost )]` | |
| `hook.Add( "PlayerSpawn", ...)`, `GM:PlayerInitialSpawn` | `Component.INetworkListener.OnActive( Connection )` | |
| `hook.Add( "PlayerDisconnected" )` | `INetworkListener.OnDisconnected( Connection )` | |
| `hook.Add( "Think" )`, `timer.Create` | `OnUpdate()`, `OnFixedUpdate()`, `TimeSince/TimeUntil`, `await Task.DelaySeconds()` | |
| `ents.Create( "prop" )`, `ENT:Initialize` | `new GameObject()` + `AddComponent<T>()`, ou `GameObject.Clone( "prefab" )` puis `NetworkSpawn()` | |
| `Player` (classe joueur), `ply:SteamID64()` | `Connection` (`SteamId`, `DisplayName`) + GameObject joueur cloné d'un prefab | Le « joueur » n'est pas une classe moteur. |
| `ply:IsAdmin()`, ULX/SAM | Système de rangs à coder nous-mêmes (données en base), `Connection.HasPermission` = hôte seulement par défaut | |
| `util.TraceLine{ start, endpos, filter }` | `Scene.Trace.Ray( from, to ).IgnoreGameObjectHierarchy( go ).Run()` | |
| `ent:TakeDamage()`, `DamageInfo()` de GMod | `Component.IDamageable.OnDamage( in DamageInfo )` + `new DamageInfo( dmg, attacker, weapon )` | Classe différente. |
| `vgui`, `Derma`, `draw.*`, `surface.*`, `HUDPaint` | UI Razor : `ScreenPanel` + `PanelComponent` (.razor + .scss) | |
| `input.IsKeyDown( KEY_E )`, `ply:KeyDown( IN_USE )` | `Input.Down( "Use" )` (client local), `connection.Down( "Use" )` (hôte) | Noms d'actions, pas de constantes `IN_*`. |
| `http.Fetch / http.Post`, `HTTP{}` | `Http.RequestJsonAsync<T>`, `Http.RequestAsync` | `HttpClient` interdit. |
| `file.Write( "data/x.txt" )`, `sql.Query` (SQLite local) | `FileSystem.Data.WriteJson( ... )` ; persistance → notre API Worker/D1 | Pas de SQLite intégré trouvé. |
| `concommand.Add`, `CreateConVar` | `[ConCmd]` / `[ConVar]` sur membres **static** | |
| `RunConsoleCommand( "kick" )` | `connection.Kick( "raison" )` | |
| `.vmt/.vtf`, `.mdl` Source 1, contenu CSS/HL2 monté | Source 2 : `.vmat`, `.vmdl`, assets sbox.game | |
| Workshop GMod, `resource.AddWorkshop` | Packages sbox.game, `PackageReferences` dans `.sbproj` | |
| Serveur `srcds` + `server.cfg` + gamemode dans `garrysmod/gamemodes` | `sbox-server.exe +game <ident ou chemin .sbproj> +hostname ...` (SteamCMD app 1892930) | |
| `os.getenv`, fichiers de config serveur Lua | Pas de variables d'environnement accessibles au code du jeu | Voir architecture-projet.md §6. |

## 2. Ancien s&box (avant 2023 : système d'entités) → supprimé

Le s&box actuel est basé sur **Scene + GameObject + Component**. L'ancienne API à base d'entités a disparu ; il n'en reste que des fichiers de test (`engine/Tests/Sandbox.Test.Unit/Data/codegen/TestRpc.Entity.cs`) qui utilisent `[ClientRpc]`, `[ServerRpc]`, `Entity`, `IClient`.

| Ancien s&box | Actuel |
|---|---|
| `class MyEntity : Entity`, `ModelEntity`, `AnimatedEntity` | `class MyComp : Component` sur un `GameObject` |
| `[Net] public int X { get; set; }`, `[Net, Predicted]` | `[Sync]` / `[Sync( SyncFlags.FromHost )]` |
| `[ClientRpc]`, `[ServerRpc]`, `[OwnerRpc]` | `[Rpc.Broadcast]`, `[Rpc.Host]`, `[Rpc.Owner]` |
| `Game.IsServer`, `Game.IsClient`, `Host.IsServer` | `Networking.IsHost`, `Networking.IsClient`, `IsProxy` |
| `IClient`, `Client.All`, `cl.Pawn` | `Connection`, `Connection.All`, notre propre lien Connection → GameObject joueur |
| `Simulate( IClient cl )`, `FrameSimulate`, `BuildInput` | `OnUpdate` / `OnFixedUpdate` + `if ( IsProxy ) return;` |
| `[Event.Tick.Server]`, `[Event.Tick]`, `[GameEvent.Tick]` | `OnUpdate`, `OnFixedUpdate`, `GameObjectSystem.Listen( Stage... )` |
| `GameManager`, `Sandbox.Game` comme classe de base du gamemode | Component « manager » dans la scène ou `GameObjectSystem<T>` |
| `Trace.Ray( a, b ).Run()` (statique) | `Scene.Trace.Ray( a, b ).Run()` |
| `RootPanel`, `HudEntity<T>` | `ScreenPanel` + `PanelComponent` |
| `Local.Pawn`, `Local.Client` | `Connection.Local`, chercher le GameObject joueur avec `Network.IsOwner` |
| `Sound.FromEntity(...)` | `Sound.Play( "event", position )` (`Systems/Audio/Sound.Play.cs`) |
| `Entity.All.OfType<T>()` | `Scene.GetAllComponents<T>()` |
| `[Library( "x" )]` pour créer des entités par nom | Prefabs + `GameObject.Clone( "chemin.prefab" )` |

## 3. API récentes mais **obsolètes** (marquées `[Obsolete]` dans le code actuel)

| Obsolète | Remplacement | Source |
|---|---|---|
| `[Broadcast]` (sans `Rpc.`) | `[Rpc.Broadcast]` | `Scene/Networking/Rpc.cs:14` |
| `[Authority]` | `[Rpc.Owner]` | `Rpc.cs:50` |
| `NetPermission.HostOnly/OwnerOnly` | `NetFlags.HostOnly / NetFlags.OwnerOnly` | `Rpc.cs:79` |
| `[HostSync]` | `[Sync( SyncFlags.FromHost )]` | `Scene/Networking/HostSync.cs:9` |
| `[Sync( Query = true )]`, `SyncFlags.Query` | assigner via le setter | `Sync.cs`, `SyncFlags.cs` |
| `List<T>`/`Dictionary` en `[Sync]` (déconseillé) | `NetList<T>`, `NetDictionary<K,V>` | `CodeUpgrader/Upgraders/SyncCollection.cs` |
| `[GameResource( ... )]` | `[AssetType( Name, Extension, Category )]` | `Resources/GameResourceAttribute.cs:83` |
| `ResourceLibrary.Get<T>( int id )` | `ResourceLibrary.Get<T>( "chemin" )` | `ResourceLibrary.cs:762` |
| `Networking.HostConnection` | `Connection.Host` | `Networking.cs:259` |
| `Network.EnableInterpolation()/DisableInterpolation()` | `Network.Interpolation = true/false` | `GameObject.Network.cs` |
| `GameNetworkSystem.IsHost` etc. | `Networking.IsHost` | |
| `LobbyConfig.AutoSwitchToBestHost` | ne fait plus rien | `LobbyConfig.cs:13` |

## 4. Pièges de logique réseau (spécifiques à s&box)

1. **`[Sync]` simple = écrit par l'owner (le client).** Le joueur possède son GameObject (via `NetworkSpawn( channel )`), donc il pourrait écrire ses propres PV s'ils étaient en `[Sync]` simple. → **Toutes les stats de gameplay en `[Sync( SyncFlags.FromHost )]`.**
2. **`Networking.IsHost` est vrai hors réseau** (solo / éditeur sans lobby). Normal pour tester, mais ne pas l'utiliser comme preuve de « serveur dédié » → `Application.IsDedicatedServer`.
3. **`#if SERVER` ne s'active que sur serveur dédié.** En test dans l'éditeur, le code `*.Server.cs` **n'est pas compilé**. Prévoir un mode de test (ex. faux client API en mémoire hors `SERVER`) sinon rien ne marche en éditeur.
4. **Le code serveur est retiré des jeux publiés** et ne marche qu'avec un projet local : le serveur dédié doit charger le **dossier du projet** (`+game chemin\.sbproj`).
5. **Le client reçoit tout le code non-serveur** : jamais de clé, d'URL d'admin ou de logique de validation sensible hors `*.Server.cs`.
6. **Un RPC `[Rpc.Host]` peut être appelé par n'importe quel client** si on n'ajoute pas `NetFlags.OwnerOnly`, et même avec, les arguments viennent du client. **Toujours revalider** (`Rpc.Caller`, distances, cooldowns, chakra) côté hôte.
7. **Après `NetworkSpawn()`, ajouter des components/enfants n'est pas répliqué** sans `Network.Refresh()`.
8. **`NetworkMode` par défaut = `Snapshot`** : les objets posés dans la scène sont envoyés une fois au join, pas synchronisés ensuite. Pour un objet vivant (PNJ, porte) : `NetworkMode.Object`.
9. **Ownership** : mettre `OwnerTransfer.Fixed` sur les joueurs, sinon (`Takeover`) un client peut prendre la propriété d'un objet.
10. **L'UI (`PanelComponent`) ne tourne pas sur le serveur dédié** (`DontExecuteOnServer`). Ne pas mettre de logique de jeu dans les .razor.
11. **Pas d'adresse IP ni d'IP privée dans `Http`** : tester le Worker en local avec `wrangler dev` exige `-allowlocalhttp` ou localhost sur 80/443/8080/8443 ; en prod utiliser le domaine `*.workers.dev` ou un domaine perso.
12. **`Authorization` est supprimé sur redirection** : appeler l'URL finale exacte (attention au `/` final et au http→https).
13. **Pas de `Task.Run` ni threads libres** : `Task.Run` n'apparaît pas dans la liste blanche (seuls `Task.Delay`, `Task.FromResult`, `ContinueWith`, etc.). Utiliser `async/await` sur les `Http.*`.
14. **Liste blanche .NET** : `System.IO.File`, `HttpClient`, `Environment.GetEnvironmentVariable`, réflexion libre → refusés à la compilation. Si le compilateur dit « not allowed », c'est ça.
15. **Migration d'hôte** existe en lobby P2P (`OnBecameHost`). Sur serveur dédié elle ne nous concerne pas, mais l'analyseur `HostAsync` peut afficher des avertissements sur l'`await` dans du code gardé par `IsHost` (désactivable via la propriété de build `SandboxHostMigration=false` — **NON VÉRIFIÉ** où la régler).
