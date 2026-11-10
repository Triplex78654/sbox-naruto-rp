using System.Threading.Tasks;
using Sandbox.Network;
using NarutoRp.Combat;
using NarutoRp.Jutsus;
using NarutoRp.Network.Api;
using NarutoRp.Player;

namespace NarutoRp.Core;

/// <summary>
/// Crée le lobby, charge le perso de chaque joueur depuis l'API puis le fait apparaître,
/// et le sauvegarde régulièrement et à la déconnexion. Tout se passe sur l'hôte.
/// </summary>
public sealed partial class GameManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }

	/// <summary>Crée un lobby au lancement si on n'est pas déjà en réseau.</summary>
	[Property] public bool StartServer { get; set; } = true;

	/// <summary>Intervalle de sauvegarde automatique.</summary>
	[Property] public float SaveIntervalSeconds { get; set; } = 120f;

	public static GameManager Current { get; private set; }

	/// <summary>API de persistance (hôte seulement, null sur les clients).</summary>
	public IApiClient Api { get; private set; }

	TimeUntil _nextSave;

	protected override void OnAwake()
	{
		Current = this;
	}

	protected override void OnDestroy()
	{
		// Fin de partie : sauvegarder tout le monde avant de disparaître (l'ordre de destruction n'est pas garanti).
		if ( Networking.IsHost )
		{
			foreach ( var ninja in Scene.GetAllComponents<NinjaPlayer>() )
				SaveCharacter( ninja );
		}

		if ( Current == this ) Current = null;
	}

	protected override void OnStart()
	{
		if ( StartServer && !Networking.IsActive )
			Networking.CreateLobby( new LobbyConfig() );

		_nextSave = SaveIntervalSeconds;

		// Domaines de clan : chargés par l'hôte (un échec API laisse les domaines vides, le jeu continue).
		if ( Networking.IsHost ) _ = NarutoRp.World.DomainRegistry.LoadAsync( GetApi() );
	}

	/// <summary>L'API est créée à la demande, quand on sait qu'on est l'hôte.</summary>
	IApiClient GetApi() => Api ??= ApiConfig.CreateClient();

	// Appelé sur l'hôte seulement, quand le client a fini de charger.
	void Component.INetworkListener.OnActive( Connection channel )
	{
		_ = LoadAndOpenMenu( channel );
	}

	async Task LoadAndOpenMenu( Connection channel )
	{
		var steamId = channel.SteamId.ValueUnsigned;

		if ( !PlayerPrefab.IsValid() )
		{
			Log.Error( "[Core] GameManager : aucun PlayerPrefab assigné" );
			Refuse( channel, "Serveur mal configuré (prefab joueur absent)." );
			return;
		}

		var api = GetApi();
		if ( api is null )
		{
			Refuse( channel, "Serveur mal configuré (API des personnages absente)." );
			return;
		}

		// Un ancien objet de ce joueur (reconnexion rapide) : on le sauvegarde et le retire avant de recharger,
		// sinon deux objets sauvegarderaient le même perso.
		foreach ( var old in Scene.GetAllComponents<NinjaPlayer>().Where( n => n.SteamId == steamId ).ToList() )
		{
			SaveCharacter( old ); // tout de suite : la destruction peut être différée
			old.GameObject.Destroy();
		}
		await WaitPendingSaves();

		PlayerDto data;
		try
		{
			data = await api.GetPlayerAsync( steamId );
		}
		catch ( Exception e )
		{
			// Ne jamais faire jouer sans ses données : une sauvegarde écraserait le vrai perso.
			Log.Warning( $"[Core] chargement de {channel.DisplayName} impossible : {e.Message}" );
			Refuse( channel, "Serveur de personnages indisponible, réessaie dans un instant." );
			return;
		}
		data.Characters ??= new();

		if ( !this.IsValid() || !channel.IsActive ) return; // parti pendant le chargement

		if ( data.ActiveBan is not null )
		{
			Log.Info( $"[Core] {channel.DisplayName} refusé : banni ({data.ActiveBan.Reason})" );
			Refuse( channel, $"Banni : {data.ActiveBan.Reason}" );
			return;
		}

		// Le joueur choisit ou crée son perso (GameManager.Characters.cs).
		OpenCharacterMenu( channel, data, null );
	}

	void Spawn( Connection channel, PlayerDto data, CharacterDto character )
	{
		if ( !PlayerPrefab.IsValid() )
		{
			Log.Warning( "[Core] GameManager : aucun PlayerPrefab assigné" );
			return;
		}

		var player = PlayerPrefab.Clone( PickSpawn( character.Village ), name: $"Ninja - {channel.DisplayName}" );
		player.Tags.Add( NetConstants.TagPlayer );

		var ninja = player.Components.Get<NinjaPlayer>();
		ninja.SteamId = channel.SteamId.ValueUnsigned;
		ninja.StaffRank = data.StaffRank;
		ApplyCharacter( ninja, character );

		// Le client contrôle son déplacement, mais seul l'hôte peut changer le propriétaire.
		// Le joueur est détruit s'il se déconnecte (NinjaPlayer.OnDestroy sauvegarde).
		player.NetworkSpawn( new NetworkSpawnOptions
		{
			Owner = channel,
			OwnerTransfer = OwnerTransfer.Fixed,
			OrphanedMode = NetworkOrphaned.Destroy,
		} );

		Log.Info( $"[Core] {channel.DisplayName} ({channel.SteamId}) est apparu : {ninja.CharacterName ?? "aucun perso"}" );
	}

	/// <summary>Remplit le joueur avec les données du perso (avant le NetworkSpawn).</summary>
	static void ApplyCharacter( NinjaPlayer ninja, CharacterDto c )
	{
		var caster = ninja.GetComponent<JutsuCaster>();
		caster?.Slots.Clear();
		if ( c is null ) return;

		ninja.CharacterId = c.Id;
		ninja.CharacterName = c.FullName;
		ninja.Village = c.Village;
		ninja.Grade = c.Grade;
		ninja.Nature = c.Nature;
		ninja.Path = c.Path;
		ninja.Clan = c.Clan;
		ninja.ClanValidated = c.ClanValidated;
		ninja.IsMedic = c.IsMedic;
		ninja.Ryo = c.Ryo;

		// Jutsus appris -> fiches. Un id inconnu (fiche supprimée) est ignoré avec un avertissement.
		if ( caster is null || c.Jutsus is null ) return;
		var byId = ResourceLibrary.GetAll<JutsuDefinition>().Where( j => j.Id is not null ).GroupBy( j => j.Id ).ToDictionary( g => g.Key, g => g.First() );
		foreach ( var id in c.Jutsus.Take( 9 ) )
		{
			var def = byId.GetValueOrDefault( id );
			if ( def is null ) Log.Warning( $"[Core] jutsu inconnu « {id} » pour le perso {c.Id}" );
			else caster.Slots.Add( def );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || !_nextSave ) return;
		_nextSave = SaveIntervalSeconds;

		foreach ( var ninja in Scene.GetAllComponents<NinjaPlayer>() )
			SaveCharacter( ninja );
	}

	/// <summary>Sauvegarde l'état modifiable du perso. Hôte seulement ; ignore les joueurs sans perso.</summary>
	public void SaveCharacter( NinjaPlayer ninja )
	{
		if ( !Networking.IsHost || Api is null || ninja.CharacterId <= 0 ) return;

		// Les sauvegardes d'un même perso partent l'une après l'autre : une ancienne valeur
		// ne peut pas arriver après une plus récente.
		var id = ninja.CharacterId;
		var previous = _pendingSaves.GetValueOrDefault( id ) ?? Task.CompletedTask;
		_pendingSaves[id] = SaveAfter( previous, id, Math.Max( 0, ninja.Ryo ), ninja.CharacterName );
	}

	readonly Dictionary<long, Task> _pendingSaves = new();

	async Task SaveAfter( Task previous, long characterId, long ryo, string name )
	{
		await previous;
		try
		{
			await Api.SaveStateAsync( characterId, ryo );
		}
		catch ( Exception e )
		{
			// L'état reste en mémoire : la prochaine sauvegarde automatique réessaiera.
			Log.Warning( $"[Core] sauvegarde de {name} échouée : {e.Message}" );
		}
	}

	/// <summary>Attend la fin des sauvegardes en cours (avant de recharger un joueur qui revient).</summary>
	Task WaitPendingSaves() => Task.WhenAll( _pendingSaves.Values.ToList() );

	/// <summary>Envoie un log à l'API sans attendre. Hôte seulement.</summary>
	public void LogEvent( string type, ulong? actor, ulong? target, object data )
	{
		if ( !Networking.IsHost || Api is null ) return;
		_ = Api.LogAsync( type, actor, target, data );
	}

	/// <summary>Expulse un joueur. L'hôte (joueur de l'éditeur) ne peut pas être expulsé : on le signale seulement.</summary>
	static void Refuse( Connection channel, string reason )
	{
		if ( channel.IsHost )
		{
			Log.Warning( $"[Core] l'hôte ne peut pas être expulsé : {reason}" );
			return;
		}
		channel.Kick( reason );
	}

	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		Log.Info( $"[Core] {channel.DisplayName} s'est déconnecté" );
		_menus.Remove( channel.Id );
	}

	/// <summary>Point d'apparition du village (groupe racine « Konoha (blocage) »…), sinon n'importe lequel.</summary>
	Transform PickSpawn( string village )
	{
		var points = Scene.GetAllComponents<SpawnPoint>().ToArray();
		if ( points.Length == 0 )
			return WorldTransform;

		var home = points.Where( p => !string.IsNullOrEmpty( village ) && p.GameObject.Root.Name.StartsWith( village, StringComparison.OrdinalIgnoreCase ) ).ToArray();
		return Game.Random.FromArray( home.Length > 0 ? home : points ).WorldTransform;
	}
}
