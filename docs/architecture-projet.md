# Architecture du projet Naruto RP (s&box)

> Version : 9 novembre 2026. S'appuie uniquement sur les API listées dans `sbox-reference.md`.
> Proposition de l'agent architecte : à valider par l'utilisateur, puis à faire évoluer dans ce fichier.

## 1. Principes

1. **Le serveur fait autorité.** Le client n'envoie que des **intentions** (`[Rpc.Host( NetFlags.OwnerOnly )] RequestXxx(...)`). L'hôte vérifie, applique, et l'état redescend par `[Sync( SyncFlags.FromHost )]` ou `[Rpc.Broadcast]` (effets visuels).
2. **Toutes les stats de gameplay sont `FromHost`** : PV, chakra, endurance, grade, village, jutsus appris, ryô, état « à terre ».
3. **Code sensible dans `*.Server.cs`** (appel API, clé secrète, validation staff) : retiré du code envoyé aux clients.
4. **Jutsus = données.** Un seul `CombatSystem` lit des fiches `JutsuDefinition`. Ajouter un jutsu = créer une fiche, pas du code.
5. **Persistance uniquement via l'API** Cloudflare Worker + D1, appelée par le serveur. Rien de persistant n'est stocké côté client.
6. **Un système = un dossier**, des dépendances simples : `UI` lit les autres systèmes mais aucun système ne dépend de `UI`.

## 2. Arborescence

```
sbox-naruto-rp/
├── narutorp.sbproj
├── Code/
│   ├── Assembly.cs                       global using Sandbox; System.Linq; ...
│   ├── Core/
│   │   ├── GameManager.cs                INetworkListener : AcceptConnection, OnActive, OnDisconnected, création lobby
│   │   ├── GameManager.Server.cs         chargement/sauvegarde via l'API (SERVER uniquement)
│   │   ├── NetConstants.cs               noms de tags, limites (distance max d'un jutsu, etc.)
│   │   └── ServerConfig.Server.cs        lecture de la config secrète (URL API + clé)
│   ├── Player/
│   │   ├── NinjaPlayer.cs                component racine du prefab joueur : lien Connection ↔ perso, SteamId
│   │   ├── NinjaInput.cs                 lit Input côté owner, appelle les RPC d'intention
│   │   ├── NinjaStats.cs                 PV / chakra / endurance en [Sync(FromHost)], IDamageable
│   │   ├── DownedState.cs                état « à terre », relevé par médic-nin
│   │   └── CharacterData.cs              DTO du perso (miroir de la table D1)
│   ├── Combat/
│   │   ├── CombatSystem.cs               GameObjectSystem<CombatSystem> : file d'actions, cooldowns, résolution
│   │   ├── CombatSystem.Validation.cs    contrôles serveur (portée, ligne de vue, chakra, grade)
│   │   ├── CastState.cs                  incantation en cours (interruptible)
│   │   ├── Effects/                      un fichier par type d'effet : DamageEffect, StunEffect, HealEffect, SlowEffect...
│   │   └── Delivery/                     un fichier par type : ProjectileDelivery, AreaDelivery, ContactDelivery, SelfDelivery
│   ├── Jutsus/
│   │   ├── JutsuDefinition.cs            [AssetType( Extension = "jutsu" )] GameResource
│   │   ├── JutsuRegistry.cs              charge toutes les fiches au démarrage (ResourceLibrary.GetAll)
│   │   └── JutsuEnums.cs                 ChakraNature, JutsuPath, JutsuRank, JutsuDeliveryType, JutsuEffectType
│   ├── Chakra/
│   │   ├── ChakraPool.cs                 régénération, concentration à l'arrêt (hôte)
│   │   └── ChakraNatureSet.cs            natures maîtrisées
│   ├── Villages/
│   │   ├── VillageDefinition.cs          [AssetType( Extension = "village" )] Konoha, Suna, Kumo
│   │   ├── VillageZone.cs                trigger (ITriggerListener) : zone protégée, frontière
│   │   └── VillageSpawnPoint.cs          point d'apparition par village
│   ├── Grades/
│   │   ├── GradeDefinition.cs            [AssetType( Extension = "grade" )] rang de jutsu max, droits
│   │   └── GradeService.Server.cs        promotion validée par le staff → API
│   ├── Staff/
│   │   ├── StaffRank.cs                  enum Moderator / Staff / Admin
│   │   ├── StaffCommands.cs              RPC d'intention staff (tp, invis, kick...) — vérif rang côté hôte
│   │   ├── StaffCommands.Server.cs       sanctions → API + logs
│   │   └── StaffPermissions.cs           table action → rang minimal
│   ├── Network/
│   │   └── Api/
│   │       ├── IApiClient.cs             interface (GetCharacter, SaveCharacter, Log...)
│   │       ├── WorkerApiClient.Server.cs implémentation Http.* + clé secrète (SERVER)
│   │       ├── MemoryApiClient.cs        faux client en mémoire pour tester dans l'éditeur
│   │       └── ApiDtos.cs                DTO JSON (pas de secrets)
│   └── UI/
│       ├── Hud.razor (+ .razor.scss)     PV / chakra / endurance
│       ├── JutsuBar.razor
│       ├── CharacterCreation.razor
│       ├── StaffPanel.razor
│       └── Nameplate.razor               WorldPanel au-dessus de la tête
├── Assets/
│   ├── scenes/                           konoha.scene (MVP), puis suna, kumo
│   ├── prefabs/                          player.prefab, projectiles, effets
│   ├── data/
│   │   ├── jutsus/<nature>/*.jutsu       une fiche par jutsu
│   │   ├── villages/*.village
│   │   └── grades/*.grade
│   ├── models/ materials/ sounds/ particles/
├── ProjectSettings/                      Input.config (touches jutsus, signes), Collision.config
├── Editor/                               outils éditeur éventuels (import CSV de jutsus...)
└── UnitTests/
```

Le backend Cloudflare vit dans `backend/` (même dépôt Git). **Vérifié** avec l'aperçu de publication s&box : ce dossier n'est pas publié avec le jeu. Schéma : `backend/migrations/0001_init.sql`, routes : `backend/README.md`.

## 3. Conventions

- **Langue** : identifiants en anglais (`NinjaStats`, `CastJutsu`), commentaires et textes de log en français.
- **Fichiers** : un type public par fichier, nom de fichier = nom du type. Partial classes : `Type.Aspect.cs` (`CombatSystem.Validation.cs`), partie serveur `Type.Server.cs`.
- **Namespace** : `NarutoRp.<Système>` (`NarutoRp.Combat`, `NarutoRp.Jutsus`...). UI : `@namespace NarutoRp.UI`.
- **Components** : `sealed` par défaut, propriétés `[Property]` pour le réglage dans l'éditeur, champs privés `_camelCase`.
- **RPC** :
  - client → hôte : `Request<Action>` (`RequestCastJutsu`, `RequestRevive`), toujours `[Rpc.Host( NetFlags.OwnerOnly )]` sauf exception documentée ;
  - hôte → tous : `Broadcast<Effet>` (`BroadcastJutsuFx`), `[Rpc.Broadcast( NetFlags.HostOnly )]` ;
  - hôte → un client : `Notify<Chose>` + `Rpc.FilterInclude( connection )` ou `[Rpc.Owner( NetFlags.HostOnly )]`.
- **Sync** : `[Sync( SyncFlags.FromHost )]` partout pour le gameplay ; `[Sync]` simple seulement pour du cosmétique piloté par le joueur (orientation de la tête...). Collections : `NetList`/`NetDictionary`.
- **Garde hôte** : en tête de toute méthode qui modifie l'état : `if ( !Networking.IsHost ) return;`. Dans le contrôle joueur : `if ( IsProxy ) return;`.
- **Identifiants de données** : `snake_case` stable (`raiton_decharge`), jamais le nom affiché.
- **Logs** : `Log.Info( $"[Combat] ..." )` avec le préfixe du système.
- **Tags** : constantes dans `NetConstants` (`"player"`, `"downed"`, `"safezone"`).

## 4. Jutsus : `GameResource` plutôt que JSON brut

**Recommandation : `GameResource` (`[AssetType]`).**

| Critère | GameResource `.jutsu` | JSON libre lu via FileSystem |
|---|---|---|
| Édition | inspecteur auto dans l'éditeur, champs typés, enums en liste | à la main, fautes de frappe |
| Références d'assets | `Model`, `SoundEvent`, `PrefabFile`, `Texture` directement en propriétés | chemins texte non vérifiés |
| Chargement | `ResourceLibrary.GetAll<JutsuDefinition>()` | code de lecture + gestion d'erreur |
| Réseau | un `GameResource` peut être argument de RPC et propriété `[Sync]` (doc) | envoyer l'id texte |
| Publication | inclus avec le jeu (s'il est référencé ou dans `Resources`) | inclus si dans Assets |
| Hotload | `PostReload()` | à coder |

Le format sous-jacent est quand même du JSON (fichier texte versionnable dans Git). Le panel web pourra lire une **copie exportée** (script ou outil éditeur) si besoin.

Point d'attention : `PublishUsedAssetsOnly` ne scanne pas le code → mettre les fiches dans un dossier `Resources/` (ex. `Assets/Resources/jutsus/`) ou lister `data/**` dans le champ `Resources` du `.sbproj`. **À vérifier** au premier test publié.

Esquisse (API vérifiées : `GameResource`, `AssetType`, `ResourceLibrary`) :

```csharp
namespace NarutoRp.Jutsus;

[AssetType( Name = "Jutsu", Extension = "jutsu", Category = "Naruto RP" )]
public sealed class JutsuDefinition : GameResource
{
	public string Id { get; set; }               // "raiton_decharge"
	public string DisplayName { get; set; }      // "Décharge foudroyante"
	public ChakraNature Nature { get; set; }     // None si voie sans nature
	public JutsuPath Path { get; set; }          // Ninjutsu, Taijutsu, Kenjutsu, Genjutsu, Medical
	public JutsuRank Rank { get; set; }          // E..S
	public float ChakraCost { get; set; }
	public float CooldownSeconds { get; set; }
	public float CastSeconds { get; set; }       // incantation, interruptible
	public float Range { get; set; }
	public JutsuDeliveryType Delivery { get; set; } // Projectile, Area, Contact, Self, Movement
	public List<JutsuEffect> Effects { get; set; } = new();
	public GameObject ProjectilePrefab { get; set; }   // NON VÉRIFIÉ : type exact pour référencer un prefab dans un GameResource (GameObject ou PrefabFile)
	public SoundEvent CastSound { get; set; }
}

public sealed class JutsuEffect      // sous-objet sérialisé dans la fiche
{
	public JutsuEffectType Type { get; set; } // Damage, Heal, Stun, Slow, Knockback
	public float Amount { get; set; }
	public float DurationSeconds { get; set; }
}
```

**VÉRIFIÉ (9 nov.)** : une `List<classe>` imbriquée se sérialise bien dans un GameResource. Code réel : `Code/Jutsus/JutsuDefinition.cs` (les champs visuels/son viendront avec le contenu, tâche 12).

Chargement :

```csharp
public sealed class JutsuRegistry : GameObjectSystem<JutsuRegistry>
{
	readonly Dictionary<string, JutsuDefinition> _byId = new();

	public JutsuRegistry( Scene scene ) : base( scene )
	{
		foreach ( var j in ResourceLibrary.GetAll<JutsuDefinition>() )
			_byId[j.Id] = j;
	}

	public JutsuDefinition Find( string id ) => _byId.GetValueOrDefault( id );
}
```

### Flux d'un jutsu

```
Client (owner)                         Hôte / serveur
──────────────                         ──────────────
NinjaInput : touche pressée
  → RequestCastJutsu( "raiton_decharge" , aimDir )
                                       CombatSystem.Validation :
                                         Rpc.Caller == owner du joueur ?
                                         jutsu appris ? grade suffisant ?
                                         chakra ≥ coût ? cooldown fini ? pas à terre ?
                                       CastState démarre (CastSeconds)
                                         → BroadcastCastStart (anim signes)
                                       fin d'incantation (si pas interrompu) :
                                         chakra -= coût, cooldown = TimeUntil
                                         Delivery (trace / zone / projectile)
                                         Effects → IDamageable.OnDamage(...) etc.
                                         → BroadcastJutsuFx (visuel + son)
NinjaStats [Sync(FromHost)] mis à jour ← PV / chakra modifiés
HUD (BuildHash) se redessine
```

## 5. Persistance : serveur → Worker → D1

```
Serveur dédié s&box  ──HTTPS + Authorization: Bearer <clé>──▶  Cloudflare Worker  ──▶  D1
(WorkerApiClient.Server.cs)                                     (valide clé + données)
```

- Appels via `Http.RequestJsonAsync<T>` / `Http.RequestAsync` avec `Http.CreateJsonContent( dto )`, en-tête `Authorization` (autorisé), timeout via `CancellationTokenSource`.
- URL : domaine public HTTPS (`xxx.workers.dev` ou domaine perso), **jamais une IP**, pas de redirection.
- Clé joueur en base : `Connection.SteamId` (`.ValueUnsigned`), jamais `Connection.Id`.
- Moments d'appel : `OnActive` → charger le(s) perso(s) ; sauvegarde périodique (ex. toutes les 2 min, `TimeUntil`) + `OnDisconnected` ; actions staff/grades → écriture immédiate + log.
- Le Worker revalide tout (bornes, droits) : le serveur s&box n'est pas la seule barrière.
- `IApiClient` permet de basculer : `WorkerApiClient` (dans `#if SERVER`) sur serveur dédié, `MemoryApiClient` sinon (éditeur, tests).

```csharp
public static class ApiClientFactory
{
	public static IApiClient Create()
	{
#if SERVER
		return new WorkerApiClient( ServerConfig.Load() );
#else
		return new MemoryApiClient();
#endif
	}
}
```

## 6. La clé secrète (point à trancher tôt)

**Décision (9 nov.)** : le code du client API (`Code/Network/Api/WorkerApiClient.cs`) est un fichier normal, il ne contient aucun secret. L'URL et la clé sont dans `api_config.json` dans `FileSystem.Data` de la machine hôte (option 1 ci-dessous), lu par `ApiConfig.CreateClient()` sur l'hôte uniquement. Sans ce fichier : `MemoryApiClient` (éditeur). Avantage : on teste le vrai client dans l'éditeur contre `wrangler dev`. Reste à vérifier : chemin de `FileSystem.Data` sur le serveur dédié.


Contraintes vérifiées : pas d'`Environment.GetEnvironmentVariable` ni de `System.IO.File` dans le code du jeu ; le code non-serveur est envoyé aux clients ; `*.Server.cs` ne l'est pas.

Options, de la plus simple à la plus propre :

1. **Fichier dans `FileSystem.Data` du serveur** (ex. `server_secrets.json`), lu par `ServerConfig.Server.cs` avec `FileSystem.Data.ReadJson<T>()`. Rien dans Git. Emplacement disque exact : **NON VÉRIFIÉ** (le trouver en loggant au premier lancement du serveur dédié).
2. **ConVar serveur** passée en ligne de commande (`+narutorp_api_key xxx`) : **NON VÉRIFIÉ** que les ConVars du jeu se règlent ainsi, et une ConVar non `Protected` est lisible par le code ; à éviter sauf si l'option 1 échoue.
3. **Constante dans un `*.Server.cs` ignoré par Git** (`.gitignore` : `Code/**/Secrets.Server.cs`) : marche, mais le fichier existe en clair sur la machine de dev.

Recommandation : **option 1**, avec l'option 3 en secours pour les premiers tests.

## 7. Ordre de mise en place (MVP)

1. Projet depuis le template player controller, scène `konoha.scene`, `GameManager` (lobby + spawn), prefab joueur (`PlayerController` + `NinjaPlayer` + `NinjaStats`), `OwnerTransfer.Fixed`.
2. `IApiClient` + `MemoryApiClient` ; création de perso en local.
3. `JutsuDefinition` + 3 fiches de test ; `CombatSystem` avec Contact puis Projectile ; `IDamageable` ; état à terre.
4. HUD Razor (PV/chakra/endurance, barre de jutsus).
5. Serveur dédié local avec projet local, `WorkerApiClient.Server.cs`, Worker + D1.
6. Outils staff de base.
