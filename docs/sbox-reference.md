# Référence API s&box (vérifiée)

> Version : 9 novembre 2026. Établie en lisant le code source public de s&box
> (`github.com/Facepunch/sbox-public`, commit `987ad085`, 9 nov. 2026) et la doc officielle `sbox.game/dev/doc`.
>
> **Règle :** tout ce qui n'est pas marqué **NON VÉRIFIÉ** a été lu dans le code source ou la doc officielle.
> Les chemins `engine/...` et `game/...` sont relatifs à la racine du dépôt `sbox-public`.
> Si tu vérifies une nouvelle API, ajoute-la ici avec sa source.
>
> Docs officielles pour agents : `https://sbox.game/llms.txt`, chaque page de doc existe en markdown en ajoutant `.md` à l'URL.
> Référence API complète : `https://sbox.game/api`.

---

## 0. Repères rapides

| Besoin | API exacte | Source |
|---|---|---|
| Suis-je le serveur / l'hôte ? | `Networking.IsHost` | `engine/Sandbox.Engine/Systems/Networking/Networking.cs:234` |
| Suis-je un client connecté (pas hôte) ? | `Networking.IsClient` | idem :239 |
| Suis-je un serveur dédié ? | `Application.IsDedicatedServer` | `engine/Sandbox.Engine/Application.cs:40` |
| Cet objet est-il simulé par quelqu'un d'autre ? | `IsProxy` (sur Component ou GameObject) | `Component.Network.cs:18` |
| Qui a appelé ce RPC ? | `Rpc.Caller` (type `Connection`) | `engine/Sandbox.Engine/Scene/Networking/Rpc.cs:102` |
| Joueur arrive / part | `Component.INetworkListener.OnActive / OnDisconnected` | `Scene/Components/Markers/INetworkListener.cs` |
| Requête HTTP | `Http.RequestJsonAsync<T>(...)`, `Http.RequestAsync(...)` | `Utility/Web/Http.Requests.cs` |
| Code serveur seulement | fichier `*.Server.cs` ou `#if SERVER` | doc « Serverside Code » + `CompilerConfiguration.cs:118` |
| Raycast | `Scene.Trace.Ray( from, to ).Run()` | `Scene/Scene/Scene.Trace.cs` |
| Dégâts | `Component.IDamageable.OnDamage( in DamageInfo )` | `Scene/Components/Markers/IDamageable.cs` |
| Fichier local | `FileSystem.Data.ReadJson<T>/WriteJson` | `Systems/Filesystem/FileSystem.cs` |

> Attention : `Networking.IsHost` vaut **true quand le réseau n'est pas actif** (partie solo / éditeur sans lobby) :
> `public static bool IsHost => System is null || System.IsHost;`

---

## 1. Components, GameObject, Scene, prefabs

### 1.1 Déclaration d'un component

Modèle officiel (`game/templates/component.cs`) :

```csharp
using Sandbox;

public sealed class MyComponent : Component
{
	protected override void OnUpdate()
	{
	}
}
```

Fichier `Code/Assembly.cs` des templates (`game/templates/game.minimal/Code/Assembly.cs`) :

```csharp
global using Sandbox;
global using System.Collections.Generic;
global using System.Linq;
```

### 1.2 Cycle de vie (méthodes `protected virtual` de `Component`)

Source : `engine/Sandbox.Engine/Scene/Components/Component.cs`, `Component.Update.cs`, `Component.Loading.cs`.

| Méthode | Quand |
|---|---|
| `OnAwake()` | création du component (une fois) |
| `OnEnabled()` / `OnDisabled()` | activation / désactivation |
| `OnStart()` | « une fois avant le premier Update, quand activé » |
| `OnUpdate()` | chaque frame, si activé |
| `OnFixedUpdate()` | à intervalle fixe = tick physique ; `Time.Delta` vaut l'intervalle fixe |
| `OnPreRender()` | avant le rendu (caméra, visuel) |
| `OnDestroy()` | destruction |
| `OnValidate()` | propriété modifiée dans l'éditeur |
| `Task OnLoad()` | chargement asynchrone (ex. `NetworkHelper` y crée le lobby) |
| `OnParentChanged( GameObject old, GameObject new )`, `OnTagsChanged()`, `OnRefresh()`, `OnDirty()` | événements divers |
| `DrawGizmos()` | gizmos éditeur |

Propriétés utiles sur `Component` : `GameObject`, `Scene`, `Components`, `Tags`, `WorldPosition`, `WorldRotation`, `WorldScale`, `WorldTransform`, `LocalPosition`..., `Network`, `IsProxy`, `IsValid`, `DebugOverlay`, `Task` (TaskSource, `protected`).
Méthodes : `Destroy()` (le component), `DestroyGameObject()`, `GetComponent<T>()`, `GetComponents<T>()`, `GetComponentInChildren<T>()`, `GetComponentInParent<T>()`, `AddComponent<T>()`, `GetOrAddComponent<T>()`.

Exemple réel (`game/samples/sweeper/code/CubeController.cs`) :

```csharp
public sealed class CubeController : Component
{
	[Property]
	public float MoveSpeed { get; set; } = 100.0f;

	protected override void OnUpdate()
	{
		var angles = WorldRotation.Angles();
		angles += Input.AnalogLook;
		angles.pitch = 0;
		angles.roll = 0;
		WorldRotation = angles;

		var moveDirection = Vector3.Zero;
		moveDirection += Input.AnalogMove * MoveSpeed * Time.Delta;
		WorldPosition += WorldRotation * moveDirection;
	}
}
```

### 1.3 Attributs d'inspecteur

- `[Property]` : exposé dans l'inspecteur et sérialisé dans la scène/prefab.
- `[RequireComponent]` : sur une propriété, récupère/ajoute automatiquement le component (`game/templates/game.playercontroller/Code/CustomTopDownController.cs` : `[RequireComponent] PlayerController Controller { get; set; }`).
- Vus dans le code : `[Range( 0, 1 )]`, `[Group( "..." )]`, `[Feature( "..." )]`, `[Category]`, `[Title]`, `[Icon]`, `[Hide]`, `[TextArea]`, `[ShowIf( "Prop", true )]`, `[InputAction]`, `[ToggleGroup]`.
- `[Change( nameof(OnXChanged) )]` : appelle `void OnXChanged( T oldValue, T newValue )` quand la propriété change (`engine/Sandbox.System/Attributes/ChangeAttribute.cs`).

### 1.4 Interfaces « marqueurs » sur Component

Source : `engine/Sandbox.Engine/Scene/Components/Markers/`.

| Interface | Effet |
|---|---|
| `Component.DontExecuteOnServer` | le component ne tourne pas sur serveur dédié (PanelComponent l'implémente) |
| `Component.ExecuteInEditor` | tourne aussi dans l'éditeur |
| `Component.INetworkListener` | événements de connexion (voir §2.6) |
| `Component.INetworkSpawn` | `OnNetworkSpawn( Connection owner )` |
| `Component.IDamageable` | `OnDamage( in DamageInfo damage )` |
| `Component.ICollisionListener` | `OnCollisionStart( Collision )`, `OnCollisionUpdate`, `OnCollisionStop( CollisionStop )` |
| `Component.ITriggerListener` | `OnTriggerEnter( Collider other )`, `OnTriggerExit`, variantes `( GameObject other )` |
| `Component.IPressable` | objets « utilisables » avec E (`Press`, `Hover`, `Look`, `Blur`...) |

### 1.5 GameObject

- Création : `new GameObject( "Nom" )`, `new GameObject( parent, enabled, name )` (`GameObject.cs:102-116`).
- Destruction : `go.Destroy()` (différée), `go.DestroyImmediate()`, `go.IsDestroyed`.
- Components : `go.AddComponent<T>()`, `go.GetComponent<T>()`, `go.Components.Get<T>( FindMode... )`.
- Tags : `go.Tags` (`ITagSet`), recherche `Scene.FindAllWithTag( "tag" )`.

### 1.6 Scene

- Scène active : `Game.ActiveScene` (`Game/Game/Game.Scene.cs:20`) ; depuis un component : `Scene`.
- Recherche : `Scene.GetAllComponents<T>()`, `Scene.GetAll<T>()`, `Scene.Get<T>()`, `Scene.FindAllWithTag( "x" )`, `Scene.Directory` (par Guid).
- Caméra principale : `Scene.Camera` (`CameraComponent`).
- `Scene.IsEditor` : vrai si on est dans la scène d'édition (pas en jeu).
- Chargement : `Scene.Load( GameResource )`, `Scene.LoadFromFile( "scenes/x.scene" )`.

### 1.7 Prefabs (clonage)

Source : `engine/Sandbox.Engine/Scene/GameObject/GameObject.Clone.cs`.

```csharp
// statiques
GameObject.Clone( string prefabPath, CloneConfig? config = default )
GameObject.Clone( string prefabPath, Transform transform, GameObject parent = null, bool startEnabled = true, string name = null )
GameObject.Clone( PrefabFile prefabFile, ... )
// d'instance (référence [Property] GameObject vers un prefab)
prefab.Clone( Transform transform, GameObject parent = null, bool startEnabled = true, string name = null )
prefab.Clone( Vector3 position ) / Clone( Vector3 position, Rotation rotation )
```

Exemple réel (`NetworkHelper.cs`) : `var player = PlayerPrefab.Clone( startLocation, name: $"Player - {channel.Name}" );`

Les prefabs sont des fichiers `.prefab` dans `Assets/`.

### 1.8 GameObjectSystem (système global par scène)

Source : `engine/Sandbox.Engine/Scene/GameObjectSystem/GameObjectSystem.cs`. Idéal pour un manager global sans GameObject.

```csharp
public sealed class MySystem : GameObjectSystem<MySystem>
{
	public MySystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, Tick, "Tick" );
	}

	void Tick() { }
}
// accès : MySystem.Current  ou  MySystem.Get( scene )
```

Stages : `StartUpdate, UpdateBones, PhysicsStep, Interpolation, FinishUpdate, StartFixedUpdate, FinishFixedUpdate, SceneLoaded, PrepareRender`.
Exemple réel : `GameObjectSystems/NetworkDebugSystem.cs`.

**NON VÉRIFIÉ** : est-ce qu'un `GameObjectSystem` dérivé est instancié automatiquement pour chaque scène (comportement attendu, vu le pattern `Current`), et comment il interagit avec le réseau (`GameObjectSystem.Network.cs` existe, non lu).

### 1.9 Événements de scène

`ISceneEvent<T>` (`Scene/Scene/Scene.Event.cs`) : déclarer une interface `public interface IMyEvents : ISceneEvent<IMyEvents> { void OnX(); }` puis `IMyEvents.Post( x => x.OnX() );` (toute la scène) ou `IMyEvents.PostToGameObject( go, x => x.OnX() )`. Exemple réel : `PlayerController.IEvents`.

`ISceneStartup` (`Scene/Events/ISceneStartup.cs`) : `OnHostPreInitialize( SceneFile )`, `OnHostInitialize()` (hôte seulement), `OnClientInitialize()` (« pas appelé sur le serveur dédié »).

### 1.10 Temps et asynchrone

- `Time.Delta`, `Time.Now`.
- `TimeSince` / `TimeUntil` (`Utility/TimeSince.cs`) : `TimeSince t = 0; if ( t > 5 ) ...` ; `TimeUntil cd = 8; if ( cd ) ...` (bool = écoulé).
- Dans un component : `await Task.DelaySeconds( 1f );` (TaskSource lié au GameObject, annulé à sa destruction). Global : `GameTask.DelaySeconds( ... )`, `GameTask.Yield()`.
- `Log.Info( "..." )`, `Log.Warning`, `Log.Error`.

---

## 2. Réseau

Doc : `https://sbox.game/dev/doc/networking.md` et sous-pages. Code : `engine/Sandbox.Engine/Scene/Networking/`, `engine/Sandbox.Engine/Systems/Networking/`.

### 2.1 Modèle

- Il y a toujours **un hôte** (host). Soit un joueur (lobby P2P), soit un **serveur dédié** (headless, `Application.IsDedicatedServer == true`).
- La doc dit que le système est « facile à utiliser » et **pas un système entièrement autoritaire serveur** par défaut : par défaut le **propriétaire (owner)** d'un objet simule son transform et écrit ses `[Sync]`. Pour un serveur qui fait autorité, il faut le construire soi-même (objets possédés par l'hôte, `SyncFlags.FromHost`, RPC `Rpc.Host` validés).
- Créer un lobby : `Networking.CreateLobby( new LobbyConfig { MaxPlayers = 64, Name = "...", Privacy = ..., Hidden = false, Password = "..." } )` (`Systems/Networking/System/LobbyConfig.cs`). Un serveur dédié doit aussi créer le lobby dans le code du jeu (doc Dedicated Servers : sinon erreur « Not Connected » côté client).
- Se connecter : `Networking.Connect( string target )` / `Connect( ulong steamid )`. Quitter : `Networking.Disconnect()`.
- Autres : `Networking.Connections` (liste), `Networking.MaxPlayers`, `Networking.ServerName`, `Networking.MapName`, `Networking.SetData/GetData`.

### 2.2 Composant tout prêt : `NetworkHelper`

Source : `engine/Sandbox.Engine/Scene/Components/Networking/NetworkHelper.cs` (code complet lu). Propriétés : `StartServer` (crée un lobby si pas déjà en réseau), `PlayerPrefab`, `SpawnPoints`. Dans `OnActive( Connection channel )` il fait :

```csharp
var player = PlayerPrefab.Clone( startLocation, name: $"Player - {channel.Name}" );
player.NetworkSpawn( channel );   // le client devient owner de son joueur
```

Il utilise les `SpawnPoint` de la scène si `SpawnPoints` est vide. Pour notre RP on écrira **notre propre** gestionnaire (chargement du perso depuis l'API avant spawn), en s'inspirant de ce code.

### 2.3 Objets réseau

Source : `Scene/GameObject/GameObject.Network.cs`, `Scene/Networking/NetworkMode.cs`.

- `NetworkMode` : `Never`, `Object` (objet réseau à part entière, owner + `[Sync]` + RPC), `Snapshot` (**défaut** : envoyé dans le snapshot initial de la scène quand un client rejoint).
- `go.NetworkSpawn()` (owner = connexion locale), `go.NetworkSpawn( Connection owner )`, `go.NetworkSpawn( bool enabled, Connection owner )`, `go.NetworkSpawn( NetworkSpawnOptions options )`.
- `NetworkSpawnOptions` : `Owner`, `OrphanedMode` (`NetworkOrphaned`), `OwnerTransfer`, `Flags` (`NetworkFlags`), `AlwaysTransmit`, `StartEnabled`.
- `go.Network` (`NetworkAccessor`) : `Active`, `IsOwner`, `OwnerId`, `Owner` (Connection, peut être null), `IsCreator`, `IsProxy`, `Flags`, `AlwaysTransmit`, `Interpolation`, `ClearInterpolation()`, `Refresh()`, `TakeOwnership()`, `AssignOwnership( Connection )`, `DropOwnership()`, `SetOrphanedMode(...)`, `SetOwnerTransfer(...)`.
- `NetworkOrphaned` (owner déconnecté) : `Destroy`, `Host`, `Random`, `ClearOwner`.
- `OwnerTransfer` : `Takeover` (n'importe qui), `Fixed` (seul l'hôte change), `Request` (demande à l'hôte). **Pour nous : `Fixed`.**
- `NetworkFlags` : `NoInterpolation`, `NoPositionSync`, `NoRotationSync`, `NoScaleSync`, `NoTransformSync`.
- Doc (networked-objects) : après `NetworkSpawn()`, ajouter/retirer des components ou des enfants **n'est pas networké** sans `Network.Refresh()`. Téléporter sans interpolation : `WorldPosition = ...; Network.ClearInterpolation();`.
- `IsProxy == true` : l'objet est simulé ailleurs (owner = autre client, ou pas d'owner et on n'est pas l'hôte). Pattern standard : `if ( IsProxy ) return;` dans `OnUpdate` du contrôle joueur.

### 2.4 Propriétés synchronisées `[Sync]`

Source : `Scene/Networking/Sync.cs`, `SyncFlags.cs` ; doc `networking/sync-properties.md`.

```csharp
[Sync] public int Kills { get; set; }                         // écrit par l'OWNER
[Sync( SyncFlags.FromHost )] public int RoundTimer { get; set; } // écrit par l'HÔTE seulement
[Sync( SyncFlags.Interpolate )] public float X { get; set; }    // interpolé (float, double, Angles, Rotation, Transform, Vector3)
[Sync, Change( nameof(OnIsRunningChanged) )] public bool IsRunning { get; set; }
private void OnIsRunningChanged( bool oldValue, bool newValue ) { }
```

- Doc : sur un proxy, une écriture `[Sync]` est **silencieusement ignorée**.
- `SyncFlags` : `FromHost = 1`, `Query = 2` (**obsolète**), `Interpolate = 4`.
- Types supportés (doc) : types valeur non managés (int, bool, float, Vector3, structs...), `string`, `GameObject`, `Component`, `GameResource`, `Connection`. **Pas de classes arbitraires.**
- Collections : `[Sync] public NetList<int> L { get; set; } = new();` et `NetDictionary<K,V>` ; événement `L.OnChanged += (NetListChangeEvent<int> e) => ...` (`Scene/Networking/Containers/`). Un analyseur (`CodeUpgrader/Upgraders/SyncCollection.cs`) recommande `NetList/NetDictionary` au lieu de `List`/`Dictionary`.
- Exemple réel moteur : `PlayerController.DefaultControls.cs:8` `[Sync( SyncFlags.Interpolate )]`.

**Pour nous (serveur autoritaire) : toutes les stats de jeu (PV, chakra, grade, village) en `[Sync( SyncFlags.FromHost )]`.**

### 2.5 RPC

Source : `Scene/Networking/Rpc.Attributes.cs`, `Systems/Networking/System/NetworkEnums.cs` ; doc `networking/rpc-messages.md`.

| Attribut | Exécuté sur |
|---|---|
| `[Rpc.Broadcast]` | tout le monde (hôte inclus) |
| `[Rpc.Host]` | l'hôte seulement |
| `[Rpc.Owner]` | l'owner de l'objet, ou l'hôte s'il n'y a pas d'owner |

Chaque attribut accepte `NetFlags` : `[Rpc.Host( NetFlags.OwnerOnly )]`.

`NetFlags` : `Unreliable` (1), `Reliable` (2, **défaut**), `SendImmediate` (4), `DiscardOnDelay` (8, unreliable seulement), `HostOnly` (16 : **seul l'hôte peut appeler**), `OwnerOnly` (32 : **seul l'owner peut appeler**), `UnreliableNoDelay`.

- Méthodes d'instance (sur un Component d'un objet networké) **ou statiques** (doc).
- Arguments : mêmes types que `[Sync]`.
- Dans le RPC : `Rpc.Caller` (Connection), `Rpc.CallerId` (Guid), `Rpc.Calling` (bool).
- Filtrer les destinataires d'un Broadcast :
  ```csharp
  using ( Rpc.FilterInclude( connection ) ) { MyBroadcastRpc(); }
  using ( Rpc.FilterExclude( c => c.DisplayName == "x" ) ) { ... }
  ```
  (`FilterInclude/FilterExclude` acceptent `Connection`, `IEnumerable<Connection>` ou `Predicate<Connection>` ; `Rpc.cs:187-272`.)

Exemple réel moteur, client → hôte avec validation (`Scene/Components/Game/Weapon/BaseCombatWeapon.HitClaims.cs:65`) :

```csharp
[Rpc.Host( NetFlags.OwnerOnly )]
private void ClaimShot( ShotClaim claim )
{
	if ( claim.Pellets is null || claim.Pellets.Length == 0 )
		return;
	if ( !float.IsFinite( claim.Damage ) || claim.Damage < 0 )
		return;
	...
}
```

**Pattern pour nous** (intention du client → vérification serveur) :

```csharp
// appelé par le client propriétaire, exécuté sur l'hôte
[Rpc.Host( NetFlags.OwnerOnly )]
public void RequestCastJutsu( string jutsuId )
{
	var caller = Rpc.Caller;           // ne jamais faire confiance aux arguments
	// vérifier : caller possède bien ce perso, jutsu appris, chakra, cooldown...
}
```

Attributs anciens **obsolètes** : `[Broadcast]`, `[Authority]`, `NetPermission` (voir pieges-gmod-vs-sbox.md).

### 2.6 Connexion des joueurs

`Component.INetworkListener` (`Scene/Components/Markers/INetworkListener.cs`), toutes méthodes « appelées seulement sur l'hôte » :

```csharp
bool AcceptConnection( Connection channel, ref string reason ) // false = refusé (whitelist, ban)
void OnConnected( Connection channel )      // quelqu'un rejoint
void OnActive( Connection channel )         // complètement chargé, en jeu → spawn ici
void OnDisconnected( Connection channel )   // quelqu'un part → sauvegarde ici
void OnBecameHost( Connection previousHost ) // migration d'hôte (P2P)
void OnHostChanged( Connection previousHost, Connection newHost )
```

`Connection` (`Systems/Networking/System/Channel/Connection*.cs`) :
- `Id` (Guid), `SteamId` (struct `SteamId`, `.Value` long / `.ValueUnsigned` ulong), `DisplayName`, `Name`, `IsHost`, `IsActive`, `IsConnecting`, `Ping`, `Latency`, `Address`, `ConnectionTime`, `PartyId`.
- `Kick( string reason )`, `SendLog( LogLevel, string )`, `HasPermission( string )` (par défaut : vrai seulement pour l'hôte), `CanSpawnObjects`, `CanRefreshObjects`, `CanDestroyObjects`.
- Entrées réseau du joueur côté hôte : `connection.Down( "Attack1" )`, `.Pressed(...)`, `.Released(...)` (`Connection.Input.cs:126-170`).
- Statiques : `Connection.Local`, `Connection.Host`, `Connection.All`, `Connection.Find( Guid )`.

**Identifiant de joueur pour la base : `Connection.SteamId`** (stable), pas `Id` (Guid de session).

Voir aussi la page doc `networking/connection-permissions` (**NON VÉRIFIÉE**, non lue).

### 2.7 Serveur dédié

Doc `https://sbox.game/dev/doc/networking/dedicated-servers.md` :

- Installation : `./steamcmd +login anonymous +app_update 1892930 validate +quit`.
- Lancement Windows : `sbox-server.exe +game facepunch.sandbox facepunch.flatgrass +hostname My Dedicated Server`.
- Paramètres : `+game <packageIdent> [map]` (ou chemin vers le `.sbproj` d'un projet local), `+hostname`, `+net_game_server_token`, `+port`, `+net_query_port`, `+net_allow_local 0|1` (client local : `connect local`).
- Connexion client : `status` côté serveur pour avoir l'ID du lobby, puis `connect <lobbyId>`.
- Lancement à forcer côté package : métadonnée `LaunchMode = "dedicatedserveronly"` → `Networking.CreateLobby` lève une exception si on n'est ni serveur dédié ni éditeur (`Networking.cs:489-492`, `Package.Info.cs:97`). **NON VÉRIFIÉ** : où on règle ce champ dans l'éditeur (probablement les paramètres de publication du projet).

### 2.8 Code serveur seulement (important pour les secrets)

Doc `https://sbox.game/dev/doc/networking/dedicated-servers/serverside-code.md` + code :

- Un fichier `Truc.Server.cs` (partial class à côté de `Truc.cs`) est entièrement enveloppé dans `#if SERVER` (`CompilerConfiguration.cs:116-118`).
- Le symbole `SERVER` n'est défini **que** quand on compile sur un serveur dédié (`Project.Compiling.cs:69-75`).
- Quand le serveur dédié envoie le code aux clients, il retire `SERVER` et supprime le texte désactivé (`Sandbox.GameInstance/GameInstanceDll.cs:330-361`) : **les clients ne reçoivent pas le code serveur**.
- Doc : « tout le code serveur est retiré des jeux publiés par défaut » et « Serverside Code ne fonctionne qu'avec des projets locaux ». Conséquence : **notre serveur dédié doit lancer le projet local** (`+game C:\chemin\vers\projet.sbproj`), pas le package publié.

```csharp
protected override void OnUpdate()
{
#if SERVER
	Log.Info( "Mise à jour serveur" );
#endif
}
```

### 2.9 ConVars / commandes console

Source : `engine/Sandbox.System/ConVar/ConVarAttributes.cs`, `ConCmdAttributes.cs`, `Sandbox.Engine/Systems/Console/Command.cs`.

- `[ConVar( "nom", ConVarFlags... )]` sur une propriété **static** ; `[ConCmd( "nom", ConVarFlags... )]` sur une méthode **static** (l'analyseur `CodeUpgrader/Upgraders/ConCmdAttribute.cs` signale l'oubli de `static`).
- Si le premier paramètre d'un ConCmd est de type `Connection`, il reçoit l'appelant (`Command.cs:228-233`).
- `ConVarFlags` : `Saved`, `Replicated` (synchronisé depuis le serveur), `Cheat`, `UserInfo`, `Hidden`, `ChangeNotice`, `Protected` (**inaccessible au code du jeu**), `Server` (exécuté sur le serveur), `Admin` (seul l'hôte peut l'exécuter : `if ( IsAdmin && !caller.IsHost )` refuse), `GameSetting`.

---

## 3. Entrées, contrôleur de joueur, caméra

### 3.1 `Input` (statique, client local)

Source : `engine/Sandbox.Engine/Systems/Input/`.

- `Input.Down( "Jump" )`, `Input.Pressed( "Attack1" )`, `Input.Released( "Use" )`.
- `Input.AnalogMove` (Vector3), `Input.AnalogLook` (Angles), `Input.MouseDelta`, `Input.MouseWheel`, `Input.MouseCursorVisible`, `Input.UsingController`, `Input.EscapePressed`.
- Touches brutes : `Input.Keyboard.Down( "f" )`, `.Pressed`, `.Released`.

Actions par défaut (`Systems/Input/Input.Common.cs`) : `Forward, Backward, Left, Right, Jump, Run, Walk, Duck, Attack1, Attack2, Reload, Use, Slot1..Slot0, SlotPrev, SlotNext, View, Voice, Drop, Flashlight, Score, Menu, Chat`.
Actions personnalisées : fichier `ProjectSettings/Input.config` (JSON, ex. `game/samples/sweeper/ProjectSettings/Input.config`), éditable dans les paramètres du projet.

Les noms d'action semblent insensibles à la casse (le moteur utilise `"Jump"` et `"jump"`) — **NON VÉRIFIÉ** formellement.

### 3.2 `PlayerController` (contrôleur intégré)

Source : `engine/Sandbox.Engine/Scene/Components/Game/PlayerController/`. `public sealed partial class PlayerController : Component` (non héritable : on l'étend avec d'autres components).

- Nécessite un `Rigidbody` (`[RequireComponent] Body`), crée `CapsuleCollider` + `BoxCollider`.
- Propriétés : `WalkSpeed` (110), `RunSpeed` (320), `DuckedSpeed`, `JumpSpeed`, `BodyRadius`, `BodyHeight`, `BodyMass`, `EyeAngles`, `EyeTransform`, `WishVelocity`, `Velocity`, `IsOnGround`, `IsAirborne`, `IsDucking`, `GroundObject`, `ThirdPerson` (true par défaut), `CameraOffset`, `HideBodyInFirstPerson`, `UseInputControls`, `UseCameraControls`, `UseAnimatorControls`, `UseLookControls`, `EnablePressing`, `UseButton` ("use"), `ReachLength`, `ToggleCameraModeButton` ("view"), `Renderer`, `Mode` (`MoveMode` : `MoveModeWalk`, `MoveModeSwim`, `MoveModeLadder`).
- Méthodes : `TraceBody(...)`, `BodyBox()`, `CreateRagdoll()`, `PreventGrounding( float )`, `UpdateAnimation( SkinnedModelRenderer )`.
- Gère déjà `IsProxy` (pas d'input ni de caméra pour les proxys).
- Événements : `PlayerController.IEvents` (`OnEyeAngles( ref Angles )`, `PostCameraSetup( CameraComponent )`, `OnJumped()`, `OnLanded( float, Vector3 )`, `StartPressing`, `StopPressing`, `FailPressing`, `PreInput`).
- Apparence : component `Dresser` (vêtements de l'avatar). Modèle citizen : `models/citizen_human/human.vmdl` (scène `game/templates/game.playercontroller/Assets/scenes/basic_example.scene` : PlayerController + Rigidbody + CapsuleCollider + BoxCollider + SkinnedModelRenderer + Dresser + MoveModeWalk/Swim/Ladder).

Exemple réel de contrôle personnalisé (`game/templates/game.playercontroller/Code/CustomTopDownController.cs`) :

```csharp
public sealed class CustomTopDownController : Component
{
	[RequireComponent] PlayerController Controller { get; set; }

	protected override void OnFixedUpdate()
	{
		var speed = Input.Down( "Run" ) ? Controller.RunSpeed : Controller.WalkSpeed;
		Controller.WishVelocity = Input.AnalogMove * speed;
	}

	protected override void OnPreRender()
	{
		Scene.Camera.WorldPosition = Controller.WorldPosition + Vector3.Up * 1024f + Vector3.Backward * 256f;
		Scene.Camera.WorldRotation = new Angles( 75, 0, 0 );
	}
}
```

Il existe aussi `CharacterController` (plus bas niveau, `Scene/Components/CharacterController/`) — **NON VÉRIFIÉ** en détail.

### 3.3 Caméra

`CameraComponent` (`Scene/Components/Camera/`) : `FieldOfView`, `IsMainCamera`, `ScreenPixelToRay( Vector2 )`, `ScreenNormalToRay( Vector3 )`. Accès : `Scene.Camera`. Positionner la caméra dans `OnPreRender()` (exemple ci-dessus).

---

## 4. Physique, traces, dégâts

### 4.1 Rigidbody / Colliders

Source : `Scene/Components/Collider/`.
- `Rigidbody` : `Velocity`, `AngularVelocity`, `Gravity`, `MotionEnabled`, `ApplyForce( Vector3 )`, `ApplyForceAt( pos, force )`, `ApplyImpulse( Vector3 )`, `ApplyImpulseAt( pos, force )`.
- Colliders : `BoxCollider`, `SphereCollider`, `CapsuleCollider`, `HullCollider`, `ModelCollider`, `PlaneCollider`. `Collider.IsTrigger` (bool), `Collider.Touching`.
- Zones (ex. zone protégée) : collider `IsTrigger = true` + component `ITriggerListener`.

### 4.2 Traces (raycasts)

Source : `engine/Sandbox.Engine/Scene/Scene/Scene.Trace.cs`. Point d'entrée : `Scene.Trace` (struct `SceneTrace`, builder).

Formes : `.Ray( from, to )`, `.Ray( Ray ray, float distance )`, `.Sphere( radius, from, to )`, `.Box(...)`, `.Capsule(...)`, `.Cylinder(...)`, `.Cone(...)`, `.Body(...)`, `.Sweep(...)`, `.FromTo(...)`, `.Size(...)`, `.Radius( r )`.
Filtres : `.WithTag( "x" )`, `.WithAllTags(...)`, `.WithAnyTags(...)`, `.WithoutTags(...)`, `.IgnoreGameObject( go )`, `.IgnoreGameObjectHierarchy( go )`, `.HitTriggers()`, `.HitTriggersOnly()`, `.IgnoreStatic()`, `.IgnoreDynamic()`, `.UseHitboxes()`, `.UseRenderMeshes()`, `.UsePhysicsWorld()`, `.WithCollisionRules(...)`.
Exécution : `.Run()` → `SceneTraceResult` ; `.RunAll()` → `IEnumerable<SceneTraceResult>`.

`SceneTraceResult` : `Hit`, `StartedSolid`, `StartPosition`, `EndPosition`, `HitPosition`, `Normal`, `Fraction`, `GameObject`, `Component`, `Collider`, `Body`, `Shape`, `Surface`, `Bone`, `Direction`, `HasTag( "x" )`, `Tags`.

Exemple réel (`BaseCombatWeapon.Shooting.cs:169`) :

```csharp
return Scene.Trace.Ray( ray, distance )
	.IgnoreGameObjectHierarchy( Attacker )
	.Radius( radius )
	.UseHitboxes();
```

### 4.3 Dégâts

- `Component.IDamageable { void OnDamage( in DamageInfo damage ); }` (`Markers/IDamageable.cs`).
- `DamageInfo` (classe, `Scene/DamageInfo.cs`) : `Attacker` (GameObject), `Weapon` (GameObject), `Hitbox`, `Damage` (float), `Origin`, `Position`, `Shape`, `Tags` (TagSet), `IsExplosion` ; constructeurs `new DamageInfo( damage, attacker, weapon )` et `( ..., hitbox )`.
- Le moteur l'utilise : `RadiusDamage.cs`, `TriggerHurt.cs`, `Prop.cs:392`. Exemple d'envoi (`RadiusDamage.cs`) : `foreach ( var d in go.GetComponentsInParent<Component.IDamageable>() ) ... d.OnDamage( damage );`.
- `BaseCombatWeapon` applique les dégâts **sur l'hôte** (`if ( Networking.IsHost ) ...` sinon « claim » envoyé à l'hôte). Notre système de combat doit faire pareil : **dégâts appliqués uniquement sur l'hôte**.

---

## 5. UI Razor

Source : `engine/Sandbox.Engine/Scene/Components/UI/`, `game/templates/default.razor`, `default.scss`.

### 5.1 Structure

- Un GameObject avec un component `ScreenPanel` (racine, HUD 2D) et un ou plusieurs `PanelComponent` (fichiers `.razor`) sur ce même objet ou ses enfants. Le `PanelComponent` se rattache au `IRootPanelComponent` du même GameObject, sinon au panel parent (`PanelComponent.cs:95-105`).
- `ScreenPanel` : `Opacity`, `Scale`, `AutoScreenScale`, `ZIndex`, `TargetCamera`.
- `WorldPanel` : panel dans le monde 3D (ex. nom au-dessus de la tête). `WorldInput` pour interagir.
- `PanelComponent` implémente `DontExecuteOnServer` : **l'UI ne tourne pas sur le serveur dédié**.

### 5.2 Fichier .razor (template officiel)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox

<root>
	<div class="title">@MyStringValue</div>
</root>

@code
{
	[Property, TextArea] public string MyStringValue { get; set; } = "Hello World!";

	/// le hash détermine si le panel doit être reconstruit
	protected override int BuildHash() => System.HashCode.Combine( MyStringValue );
}
```

- **Mise à jour** : le panel se reconstruit quand `BuildHash()` change → y mettre toutes les valeurs affichées (PV, chakra...). Ou forcer avec `StateHasChanged()`.
- Événements : `onclick=@Methode`, `onclick=@(e => Faire( e, x ))`, `onrightclick=...` (vus dans `game/addons/menu/Code/`).
- Hooks : `OnTreeFirstBuilt()`, `OnTreeBuilt()`, `OnMouseDown/Move/Up/Over/Out( MousePanelEvent )`, `OnMouseWheel( Vector2 )`. Accès au panel : `Panel`.

### 5.3 Styles SCSS

- Fichier `MonPanel.razor.scss` à côté de `MonPanel.razor` : chargé **automatiquement** (`PanelComponent.cs:162-187` : chemin de la classe + `.scss`).
- Autre feuille : `@attribute [StyleSheet( "/chemin/fichier.scss" )]` dans le razor ou `[StyleSheet( "x.scss" )]` sur une classe.
- Syntaxe CSS/SCSS (flexbox par défaut, `position: absolute`, `justify-content`, `align-items`, `font-family`, `border-radius`...). Exemple : `game/templates/default.scss`. Le sous-ensemble exact de CSS supporté est **NON VÉRIFIÉ** (voir doc UI officielle).

---

## 6. Requêtes HTTP sortantes

Source : `engine/Sandbox.Engine/Utility/Web/Http.cs`, `Http.Requests.cs`. Doc : page `networking/http-requests` (non lisible ici, contenu vérifié dans le code).

### 6.1 API

```csharp
Task<string>              Http.RequestStringAsync( string uri, string method = "GET", HttpContent content = null, Dictionary<string,string> headers = null, CancellationToken ct = default )
Task<byte[]>              Http.RequestBytesAsync( ... )
Task<Stream>              Http.RequestStreamAsync( ... )
Task<T>                   Http.RequestJsonAsync<T>( ... )   // lève HttpRequestException si statut non 2xx
Task<HttpResponseMessage> Http.RequestAsync( ... )          // ne lève pas sur statut, à tester soi-même
HttpContent               Http.CreateJsonContent<T>( T target )  // application/json
bool                      Http.IsAllowed( Uri uri )
bool                      Http.IsHeaderAllowed( string header )
```

Exemple pour notre Worker (code à mettre dans un `*.Server.cs`) :

```csharp
var headers = new Dictionary<string, string> { { "Authorization", $"Bearer {secret}" } };
var perso = await Http.RequestJsonAsync<CharacterDto>(
	$"{baseUrl}/characters/{steamId}", "GET", null, headers );

using var resp = await Http.RequestAsync(
	$"{baseUrl}/characters", "POST", Http.CreateJsonContent( dto ), headers );
if ( !resp.IsSuccessStatusCode ) { Log.Warning( $"API {resp.StatusCode}" ); }
```

Types autorisés pour le code de jeu (liste blanche `engine/Sandbox.Compiling/Compiler/Access/Rules/`) : `HttpResponseMessage`, `HttpContent`, `StringContent`, `ByteArrayContent`, `FormUrlEncodedContent`, `JsonContent`, `HttpStatusCode`, `HttpRequestException`, `System.Text.Json.JsonSerializer` et compagnie. **`HttpClient` n'est pas dans la liste** : utiliser `Http.*`.
Sérialisation : `Json.Serialize( obj )`, `Json.Deserialize<T>( string )` (`Utility/Json/Json.cs`) ou `System.Text.Json.JsonSerializer`.

### 6.2 Restrictions (vérifiées dans `Http.cs`)

- Schémas : `http`, `https`, `ws`, `wss`.
- **Pas d'adresse IP directe** dans l'URL, et le domaine **ne doit pas résoudre vers une IP privée/loopback**. Exception : `localhost` sur ports 80/443/8080/8443, ou serveur dédié lancé avec `-allowlocalhttp`, ou jeu standalone. **Vérifié (9 nov.) : dans l'éditeur, `http://localhost:8788` est refusé (« Access ... is not allowed »), `http://localhost:8080` marche.** → pour tester contre `wrangler dev`, utiliser le port 8080.
- **Pas de liste blanche de domaines** : n'importe quel domaine public (ex. `*.workers.dev`) est accepté.
- En-têtes interdits : `User-Agent` (forcé à `facepunch-sbox`), `Referer` (forcé), `Host`, `Origin`, `Content-Length`, `Connection`, tout `Proxy-*` et `Sec-*`, etc. **`Authorization` et les en-têtes personnalisés (`X-Api-Key`) sont autorisés.**
- Pas de requêtes synchrones. Redirections suivies manuellement : `Authorization` **supprimé** à chaque redirection, pas de passage https → http. → Appeler directement l'URL finale du Worker.
- Timeout du client : 120 min → passer un `CancellationToken` pour un vrai timeout : `using var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 10 ) );` (`CancellationTokenSource` est dans la liste blanche, `Access/Rules/BaseAccess.cs:42`).
- **`Http` est utilisable côté client comme côté serveur.** Rien n'empêche un client d'appeler l'API : la protection vient uniquement de la clé secrète, qui ne doit exister que dans le code/les fichiers serveur.

### 6.3 WebSocket

`Sandbox.WebSocket` (`Utility/WebSocket.cs`) : `new WebSocket()`, `Connect( uri )`, `Connect( uri, headers )`, `Send( string | byte[] )`, événements `OnMessageReceived`, `OnDataReceived`, `OnDisconnected`, `IsConnected`.

---

## 7. Stockage local

Source : `engine/Sandbox.Engine/Systems/Filesystem/FileSystem.cs`, `engine/Sandbox.Filesystem/BaseFileSystem.cs`.

- `FileSystem.Data` : dossier de données du jeu (sous-dossier de `FileSystem.OrganizationData`, par org/package). `FileSystem.Cache` : KeyStore pouvant être effacé à tout moment.
- Méthodes : `ReadAllText`, `ReadAllTextAsync`, `WriteAllText`, `ReadJson<T>( file, default )`, `ReadJsonOrDefault<T>`, `WriteJson<T>( file, data )`, `FileExists`, `DirectoryExists`, `CreateDirectory`, `FindFile( folder, pattern, recursive )`.
- `Game.Cookies` (`CookieContainer`, stocké dans `FileSystem.Data`) pour de petites préférences.
- Données **locales à la machine** qui exécute le code : sur le client = préférences UI ; sur le serveur dédié = fichiers du serveur. **Jamais pour la persistance des persos** (c'est l'API D1).
- `System.IO.File` et `System.Environment.GetEnvironmentVariable` **ne sont pas** dans la liste blanche (seuls `Environment.CurrentManagedThreadId` et `Environment.StackTrace` le sont, `Access/Rules/BaseAccess.cs:44-45`). → **On ne peut pas lire une variable d'environnement depuis le code du jeu.**
- Emplacement disque exact de `FileSystem.Data` sur le serveur dédié : **NON VÉRIFIÉ** (racine `EngineFileSystem.Data`, sous-dossier `<org>/<package>` ou `.local/<ident>` pour un projet local, `GameInstanceDll.cs:820-845`). À trouver en loggant / en cherchant le dossier `data` du serveur.

---

## 8. Structure de projet et publication

### 8.1 Dossiers (source : `Systems/Project/Project/Project.cs:309-393`)

```
MonJeu/
├── monjeu.sbproj          ← fichier projet (sa présence fait du dossier un projet)
├── Code/                  ← C# et .razor/.scss du jeu (compilé, envoyé aux clients sauf *.Server.cs)
├── Editor/                ← outils éditeur (ignoré à la publication du jeu)
├── UnitTests/             ← tests (ignoré à la publication)
├── Assets/                ← scènes (.scene), prefabs (.prefab), modèles, matériaux, sons, GameResources
├── Localization/          ← traductions
├── ProjectSettings/       ← Input.config, Collision.config...
└── Libraries/             ← bibliothèques installées
```

### 8.2 `.sbproj` (template `game/templates/game.minimal/$ident.sbproj`)

```json
{
  "Title": "Game - Empty",
  "Type": "game",
  "Org": "local",
  "Ident": "game.minimal",
  "Schema": 1,
  "HasAssets": true,
  "AssetsPath": "",
  "PublishUsedAssetsOnly": true,
  "HasCode": true,
  "CodePath": "/code/",
  "PackageReferences": [],
  "Metadata": {
    "MaxPlayers": 64,
    "MinPlayers": 1,
    "TickRate": 50,
    "GameNetworkType": "Multiplayer",
    "MapSelect": "Unrestricted",
    "StartupScene": "scenes/minimal.scene"
  }
}
```

- `PublishUsedAssetsOnly` : ne publie que les assets référencés par les scènes/paramètres, le dossier `Resources`, et les motifs `Resources` (`ProjectConfig.cs:84-95`). « Le code n'est pas analysé » → un asset chargé seulement par chemin dans le code doit être listé dans `Resources` ou placé dans un dossier `Resources/`.
- Types : `game`, `addon`, `library`, `tool`.
- Code de jeu compilé avec une **liste blanche** d'API .NET (pas d'`unsafe`, pas de `System.IO.File`, etc.), sauf projets `IsStandaloneOnly` (`Project.Compiling.cs:84-91`).
- Création : éditeur s&box → New Project (templates « Game - Empty », player controller...). Publication : via l'éditeur vers sbox.game (org à créer sur sbox.game). Détails du processus de publication : **NON VÉRIFIÉ** (code dans `engine/Sandbox.Tools/Utility/ProjectPublisher/`, non lu en détail).

---

## 9. GameResource (fiches de données)

Source : `engine/Sandbox.Engine/Resources/GameResource.cs`, `GameResourceAttribute.cs`.

Exemple réel moteur (`Scene/Components/Game/Weapon/BaseAmmoResource.cs`) :

```csharp
[AssetType( Name = "Ammo Type", Extension = "ammo", Category = "Game" )]
public class BaseAmmoResource : GameResource
{
	public virtual string Title { get; set; }
	public virtual Texture Icon { get; set; }
	public virtual int MaxReserve { get; set; } = 120;
}
```

- `[AssetType( Name, Extension, Category, Flags, IconColor )]` (l'ancien `[GameResource(...)]` est **obsolète**).
- Les fichiers sont du JSON créés/édités dans l'éditeur (inspecteur auto), stockés dans `Assets/`.
- Chargement : `ResourceLibrary.Get<T>( "chemin/fichier.ext" )`, `ResourceLibrary.TryGet<T>( path, out var r )`, `ResourceLibrary.GetAll<T>()`, `ResourceLibrary.GetAll<T>( folder, recursive )` (`Resources/ResourceLibrary.cs:755-798`). `Get<T>( int )` est obsolète.
- Hooks : `PostLoad()`, `PostReload()` (hotload), `OnJsonSerialize/OnJsonDeserialize( JsonObject )`, `ResourceVersion`.
- Un `GameResource` peut être une propriété `[Sync]` (doc sync-properties) et un argument de RPC.

---

## 10. Points NON VÉRIFIÉS (récapitulatif)

1. Lecture d'une clé secrète au démarrage du serveur : pas de variables d'environnement. Pistes : fichier dans `FileSystem.Data` du serveur lu par du code `*.Server.cs` (API vérifiée, chemin disque non vérifié) ; ou ConVar non répliquée passée en ligne de commande `+nom valeur` (passage `+convar` en ligne de commande **non vérifié** pour les ConVars du jeu). À tester tôt.
2. ~~Création automatique des `GameObjectSystem`~~ VÉRIFIÉ (doc XML : « always exists in every scene »). Comportement réseau toujours non vérifié.
3. Champ `LaunchMode = dedicatedserveronly` : où le régler.
4. Emplacement disque de `FileSystem.Data` sur le serveur dédié.
5. Sous-ensemble CSS supporté par l'UI.
6. Processus de publication détaillé.
7. Pages doc non lues : connection-permissions, network-visibility, host-migration, ownership.
8. Insensibilité à la casse des noms d'actions d'input.

## 11. Vérifié dans l'éditeur (moteur 26.11.03a, 9 nov. 2026)

Testé en direct via `sbox-cli` (pont MCP de l'éditeur) :

- **GameResource avec `List<classe>` imbriquée** : se sauvegarde et se recharge correctement (fiches `.jutsu` avec `List<JutsuEffect>`). Les enums sont sérialisés par leur nom (`"Delivery": "Projectile"`).
- **`NetList<JutsuDefinition>` en `[Property]`** dans un prefab : sérialisé comme une liste de chemins (`"data/jutsus/test/test_contact.jutsu"`), rechargé correctement.
- **Référence de prefab dans un component** : propriété `GameObject`, sérialisée `{ "_type": "gameobject", "prefab": "prefabs/player.prefab" }`.
- **Position possédée par le client** : l'hôte **ne peut pas** réécrire `WorldPosition` d'un objet dont l'owner est un client (la valeur du client écrase celle de l'hôte). Tout blocage de déplacement (à terre, étourdi) est donc appliqué côté client ; l'hôte ne peut que refuser les actions.
- `Vector3.IsValid()` **n'existe pas** : tester `float.IsFinite` sur x, y, z.
- `System` n'est pas dans les `global using` du template : `MathF`/`Math` demandent `global using System;` (ajouté dans `Code/Assembly.cs`).
- `PlayerController.EyePosition`, `EyeAngles`, `Velocity`, `WalkSpeed`, `RunSpeed`, `UseInputControls` : existent.
- `GameObject.Clone( Transform, name: ... )` et `NetworkSpawn( NetworkSpawnOptions )` avec `OwnerTransfer.Fixed` + `NetworkOrphaned.Destroy` : fonctionnent (2 clients testés).
- **Piège éditeur** : modifier un `.prefab` sur le disque pendant que l'éditeur l'a en cache peut casser le cache (« Assert: AreEqual N components weren't deleted »). Les objets clonés perdent alors des components. Correctif : modifier à nouveau le fichier (le moteur le recharge proprement).

- **Piège prefab (bis)** : à chaque modification du `.prefab` sur disque, le premier rechargement casse le cache ; une 2e modification (même un saut de ligne) le répare. Vérifier avec `ResourceLibrary.Get<PrefabFile>( path ).GetScene()`.
- **Nouvelles actions d'input** : ajoutées dans `ProjectSettings/Input.config` (JSON, champs `Name`, `GroupName`, `Title`, `KeyboardCode`, `GamepadCode`), elles ne sont prises en compte qu'au redémarrage de l'éditeur (le runtime `Input` lit la config au chargement du projet). `Input.ReadConfig( InputSettings )` existe mais n'est pas public (appelable par réflexion depuis `run_csharp` pour éviter le redémarrage).
- `Rigidbody.Velocity` est modifiable : utilisé pour l'élan d'esquive, appliqué par le propriétaire.
- **Impulsion verticale** (bond) : `body.Velocity += Vector3.Up * …` seul ne décolle pas, le `PlayerController` se recolle au sol à la frame suivante (hauteur mesurée : 0). Appeler `Controller.PreventGrounding( 0.3f )` avant (mesuré : ~100 unités de haut pour 420 vers le haut). `PlayerController.Jump( Vector3 )` existe aussi (même résultat).
- **Tests via `run_csharp`** : `await Task.Yield()` n'attend pas une frame ; pour mesurer un mouvement, boucler avec `await Task.Delay( 10 )` et un `Stopwatch`.

- **`FileSystem.Data` dans l'éditeur** : `D:\SteamLibrary\steamapps\common\sbox\data\local\nrp#local` (projet local `nrp`). `FileSystem.Data.WriteJson / ReadJson / DeleteFile / FileExists / GetFullPath` vérifiés.
- **`Http.RequestAsync`** + `Http.CreateJsonContent( objetAnonyme )` + `[JsonPropertyName]` (System.Text.Json) + `Json.Deserialize<T>` : vérifiés de bout en bout contre le Worker local (chargement, sauvegarde, logs).
- `Connection.Kick( raison )` et `Connection.IsActive` : vérifiés (joueur banni refusé).

- **RPC statiques** : `[Rpc.Host] static void X(...)` appelée par un client sans objet en jeu fonctionne, `Rpc.Caller` identifie l'appelant ; `[Rpc.Broadcast( NetFlags.HostOnly )] static` + `using ( Rpc.FilterInclude( connection ) )` n'atteint que ce client (vérifié : l'état de l'hôte n'est pas modifié quand un 2e client reçoit son menu).
- **Composant ou GameResource en argument de RPC** (`NinjaPlayer target`, `JutsuDefinition jutsu`) : fonctionne.
- `Connection.Kick` sur l'hôte lui-même : assertion moteur (« Object should not be null ») → ne jamais expulser `channel.IsHost`.
- `ModelRenderer.Tint` (couleur avec alpha) et `Enabled` pour cacher un joueur ; `PlayerController.UseCameraControls = false` pour placer soi-même `Scene.Camera` (spectateur).
- Tags posés par l'hôte + `Scene.Trace...WithoutTags( "tag" )` : la trace ignore l'objet (vérifié pour le staff invisible).
- UI Razor : `<TextEntry Value:bind=@champ MaxLength=@(20) Placeholder="..." Numeric=@true />`, `<Button Active=@bool disabled=@bool onclick=@(() => ...)>`, `pointer-events: all` sur la racine affiche la souris. Texte avec séparateurs : utiliser `@($"{a} · {b}")` (sinon les espaces autour des `·` sont mangés).
- `System.Text.RegularExpressions.Regex` est autorisé dans le code de jeu.

- **Piège sauvegarde de scène** : une scène modifiée par `run_csharp` n'est pas marquée « modifiée », et pour une scène dont le fichier a été créé hors de l'éditeur, `save_scene` / `SceneEditorSession.Save` répondent « sauvegardé » **sans écrire le fichier**. Contournement : `scene.Serialize()["GameObjects"]` puis réécrire ce tableau dans le `.scene` (en gardant l'en-tête du fichier). Vérifier la date du fichier après une sauvegarde.
- **Piège fiches modifiées hors éditeur** : modifier un `.jutsu` sur le disque recompile bien le `_c`, mais l'instance chargée (`ResourceLibrary`) garde les anciennes valeurs jusqu'au redémarrage de l'éditeur ; une fiche supprimée reste chargée aussi. Créer avec `create_asset` marche à chaud.
- **Projet** : `Project.Current.Config.SetMeta( "StartupScene", ... )`, propriété `Resources` (texte de règles, ex. `data/jutsus/*`), puis la méthode `Save` de `Project.Current` (par réflexion) écrit le `.sbproj`. `publishing_preview { "resources": "..." }` permet de tester une règle avant.
- Effets : `new GameObject( true, nom ) { NetworkMode = NetworkMode.Never }` + `ModelRenderer` (`models/dev/sphere.vmdl`, `models/dev/box.vmdl` = cube de 50 unités, `models/dev/plane.vmdl` = 100 × 100) + `PointLight` (`LightColor`, `Radius`, `Shadows`). `Sound.Play( SoundEvent, Vector3 position, float fadeIn = 0 )`. Sons utilisables du moteur : `sounds/impacts/melee/*`, `sounds/effects/explosion/explosion_small.sound`, `sounds/water/water_splash_medium.sound`.
- **Clavier** : les actions sont liées à la position physique de la touche (disposition QWERTY). Sur AZERTY, `"z"` = touche W, `"w","a"` = Z, Q. `Input.GetButtonOrigin( action )` renvoie le nom réel à afficher.

### Piloter l'éditeur (`sbox-cli`)

`<engine>/bin/managed/sbox-cli.exe`, lancé depuis le dossier du projet : `--call editor_status`, `compile_status`, `play_start/stop`, `network_spawn_instance` (2e client local), `run_csharp` (code C# exécuté dans l'éditeur), `create_asset`, `scene_tree`, `add_component`, `save_scene`. Instructions complètes : `sbox-cli --skill sbox-cli`.

### Terrain (vérifié 2026-11-10, éditeur 26.11.03a)

- Asset : `create_asset {"type":"terrain","path":"terrain/monde.terrain"}` puis `Editor.AssetSystem.FindByPath( ... ).LoadResource<TerrainStorage>()`, modification, `asset.SaveToDisk( storage )`. Un `Terrain` dont le `Storage` n'est pas un asset est sérialisé `"Storage": null`.
- `TerrainStorage` : `SetResolution( int )`, `HeightMap` (`ushort[]`, ligne y puis x, 0..65535 = 0..`TerrainHeight`), `ControlMap` (`uint[]`), `TerrainSize`, `TerrainHeight` (u), `Materials` (`List<TerrainMaterial>`, vide par défaut). Trou : `new CompactTerrainMaterial( 0, 0, 0, true ).Packed` (= 262144) dans `ControlMap` ; rendu ET collision respectent le trou (trace verticale qui traverse).
- Le terrain démarre à sa position (coin) et s'étend de `TerrainSize` : `Relief` à (-56000, -56000, -2000), taille 112 000, hauteur 40 000 -> z monde = -2000 + hauteur stockée.
- `Terrain` : `Storage = storage; Create();` construit rendu + collision. Hauteur relue par trace : exacte au centimètre près.
- Couleur : `TerrainMaterial` est une ressource avec `AlbedoImage` / `NormalImage` / ... (chemins de textures) : pas de couleur unie sans texture. Sans matériau (`Storage.Materials` vide), rendu = grille de dev blanche (relief peu lisible de loin). Avec 3 `.tmat` faits d'images unies de 16 px (`Assets/terrain/{herbe,sable,roche}`, `{"AlbedoImage":"terrain/herbe.png",...}`) indexés par `new CompactTerrainMaterial( index, 0, 0, false ).Packed` dans `ControlMap` : `ResourceLibrary.Get<TerrainMaterial>( "terrain/x.tmat" )` charge bien le matériau. Couleurs : voir « Matériau de terrain (`.tmat`) » ci-dessous.
- **Matériau de terrain (`.tmat`)** (vérifié 2026-11-10 dans le source public `engine/Sandbox.Tools/Assets/TerrainMaterialCompiler.cs`, `engine/Sandbox.Engine/Resources/Terrain/TerrainMaterial.cs` et `core/shaders/terrain/TerrainSplat.hlsl`) : le compilateur lit le **JSON brut** et fabrique deux textures, `<nom>_tmat_bcr.generated.vtex` (couleur RGB + rugosité A, à partir de `AlbedoImage` + `RoughnessImage`) et `<nom>_tmat_nho.generated.vtex` (normale RG, hauteur B, AO A, à partir de `NormalImage` + `HeightImage` + `AOImage`). Le shader prend la couleur du sol **uniquement** dans `bcr` (`o.Albedo = SrgbGammaToLinear( bcr.rgb )`). Une clé absente du JSON vaut `""` (les valeurs par défaut C# ne s'appliquent pas à la compilation) → « Error reading texture "" for "roughness" » → la texture `bcr` entière échoue → sol gris uni. **Écrire les 5 images** dans chaque `.tmat` : `{"AlbedoImage":"terrain/herbe.png","RoughnessImage":"materials/default/default_rough.tga","NormalImage":"materials/default/default_normal.tga","HeightImage":"materials/default/default_ao.tga","AOImage":"materials/default/default_ao.tga","UVScale":1,"Metalness":0}` (valeurs par défaut de `TerrainMaterial`). Réécrire le fichier sur le disque suffit : l'éditeur le recompile à chaud et le Play suivant montre les couleurs, sans relancer `terrain-import` ni réenregistrer la scène (`monde.terrain` référence les `.tmat` par chemin). Vérifié en Play : forêt verte, désert sable, roche gris-brun, neutre terre ; fondu visible aux frontières.
- **Collision par en dessous** (vérifié T6) : le heightfield n'existe que par le dessus. Une trace vers le haut depuis un tunnel ne le touche pas, et un joueur lancé vers le haut (900 u/s) le traverse et ressort sur le relief. Tout souterrain doit donc avoir son propre plafond (les puits ont un « toit du puits » depuis T6). Pour trouver le plafond au-dessus d'un point : trace vers le bas depuis le ciel.
- Fichiers : `monde.terrain` (JSON, `"Maps": { "$blob": ... }`) + `monde.terrain_d` (données hauteurs/contrôle, ~190 Ko, versionné) ; `monde.terrain_c` est compilé (ignoré par Git).
- Autres API vérifiées (compilation à blanc d'un script `run_csharp`, 2026-11-10) : `Rotation.LookAt( Vector3 forward, Vector3 up )` (boîte orientée selon une direction, x = avant), `GameObject.Tags.Has( "x" )` / `Tags.Add( "x" )` (marque posée par un script pour le rendre relançable), `scene.GetAllObjects( true )`, `GameObject.Children` (liste) et `Components.Get<T>()`. `List<>` n'est pas importé par défaut dans `run_csharp` : écrire `System.Collections.Generic.List<>`.

### Carte des territoires (vérifié T6 territoires, 2026-11-10, éditeur 26.11.03a)

- **Sol par biome** : `Storage.Materials` = 4 `.tmat` (`herbe`, `sable`, `roche`, `neutre`) ; chaque case de `ControlMap` = `new CompactTerrainMaterial( (byte)base, (byte)dessus, (byte)mélange, false ).Packed` (mélange 0..255 entre base et dessus, compile et se relit avec `new CompactTerrainMaterial( uint )` → `BaseTextureId`, `OverlayTextureId`, `BlendFactor`, `IsHole`). Couleurs visibles depuis que les `.tmat` listent les 5 images (voir ci-dessus). Avec le mélange, `monde.terrain_d` passe de ~190 Ko à ~2,4 Mo.
- `create_asset {"type":"tmat","path":"terrain/neutre.tmat","json":"{\"AlbedoImage\":\"terrain/neutre.png\",\"UVScale\":1,\"Metalness\":0}"}` : crée et compile le matériau à chaud (**piège** : sans `RoughnessImage` / `NormalImage` / `HeightImage` / `AOImage` dans le JSON, les textures générées échouent et le sol reste gris ; voir ci-dessus).
- `ModelRenderer.Tint` avec alpha (`new Color( r, g, b, 0.55f )`) sur `models/dev/box.vmdl` : rendu **translucide** (tramé) — eau des rivières, chutes. Une fonction locale avec paramètre par défaut (`bool collide = true`) compile dans `run_csharp`.
- `docs/monde-blocage-v3.cs.txt` (11 873 objets) s'exécute en ~2 s ; relancé une 2e fois : même arbre, mêmes positions et échelles (comparaison objet par objet). Scène écrite : ~30 Mo.
- **Recharger la scène depuis le disque** : `open_scene` ne recharge pas un onglet déjà ouvert ; `close_scene {"scene":"scenes/konoha.scene","discardChanges":true}` puis `open_scene`.
- Dans `run_csharp` hors Play, `Game.ActiveScene` est **null** : prendre `Editor.SceneEditorSession.Active.Scene` (les tests qui marchent dans les deux cas : `Game.IsPlaying ? Game.ActiveScene : ...`).
- `SceneTrace.IgnoreGameObjectHierarchy( go )`, `GameObject.WorldTransform.PointToWorld( Vector3 )`, `GameObject.GetBounds()` (lent) : utilisés par `tools/tests/ground-check.cs`.
- Libérer les emplacements de persos de l'API mémoire (éditeur) : vider par réflexion le champ statique privé `MemoryApiClient.Players` hors Play, puis Play et `GameManager.RequestCreateCharacter`.
- i/s avec ~11 900 objets (dont ~7 300 à Konoha) : mesure = `RealTime.Now` relu après chaque `await Task.Delay( 1 )`, minimum = plus grand écart sur 2 s ; joueur figé avec `Rigidbody.Gravity = false` + position réécrite. Laisser **10 s de chauffe** après une téléportation (sinon une image de ~125 ms). En régime établi : ~171 moyen, minimum 57-85 selon la fenêtre, identique sans territoires ni végétation (`GameObject.Enabled = false` en Play) et sans le `Terrain` (`Component.Enabled = false`) ; scène vidée : ~115-128 moyen, minimum 57. Des arrêts isolés (25-143 ms, ~1 fenêtre sur 10) existent dans tous les cas, scène vide comprise : coût de l'éditeur, pas du contenu.
- **Piège prefab pendant le Play** : modifier un prefab (ou un asset utilisé par les objets vivants) pendant le Play corrompt le cache de prefab de l'éditeur (« N components weren't deleted », puis `NullReferenceException` dans `PlayerController.EnsureComponentsCreated` à chaque apparition). Toujours arrêter le Play avant de toucher un asset.

### Portes (LockedDoor, vérifié 2026-11-10)

- `[Rpc.Host]` d'instance : `Rpc.Caller` identifie l'appelant ; sur l'hôte local `Rpc.Caller` est bien renseigné (testé avec `tools/tests/door-check.cs`). Un objet de scène en mode `Snapshot` (défaut) n'est pas un objet réseau : pour que `[Sync( FromHost )]` et les RPC d'instance marchent, mettre `NetworkMode = NetworkMode.Object` (le générateur le fait pour les portes ; `NetworkSpawn()` sur un objet créé à l'exécution donne `Network.Active == true`). **Vérifié avec un vrai 2e client** (T6, `network_spawn_instance`, vraie carte, portes de scène en `NetworkMode.Object`) : le client appelle `RequestOpen` -> l'hôte reçoit le RPC avec le bon `Rpc.Caller` (client genin refusé, client Kage du village accepté) ; chez le client, `IsOpen` passe à vrai, le battant disparaît puis revient après 4 s ; un client qui rejoint pendant qu'une porte est ouverte la voit ouverte (battant inactif) ; `OwnerClan` posé par l'hôte est lu par le client. Les instances locales ont un SteamId factice distinct de l'hôte (ex. 90071996842377217). Méthode : composant de test temporaire avec un `[Rpc.Broadcast( NetFlags.HostOnly )] static` qui fait agir le client et écrit le résultat dans son log (`<sbox>/logs/sbox.log`), supprimé après le test.
- Sens unique réel (T6) : sur T1/T2 des 3 villages, l'avant de la porte pointe vers la salle ; un Kage non staff ouvre côté salle, est refusé côté tunnel.
- `GameObject.IsDescendant( GameObject )` utilisé (compile). Le Shunshin ignore seulement le tag `invisible` : le battant (sans tag) l'arrête, vérifié par trace réelle.
