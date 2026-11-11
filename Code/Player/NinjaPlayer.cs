using NarutoRp.Core;

namespace NarutoRp.Player;

/// <summary>
/// Component racine du prefab joueur : identité du perso chargé depuis l'API.
/// Tout est écrit par l'hôte uniquement.
/// </summary>
public sealed class NinjaPlayer : Component
{
	/// <summary>Clé du joueur en base (stable), jamais Connection.Id.</summary>
	[Sync( SyncFlags.FromHost )] public ulong SteamId { get; set; }

	/// <summary>Id du perso en base ; 0 = aucun perso chargé (rien n'est sauvegardé).</summary>
	[Sync( SyncFlags.FromHost )] public long CharacterId { get; set; }

	/// <summary>Nom RP « Prénom Nom ».</summary>
	[Sync( SyncFlags.FromHost )] public string CharacterName { get; set; }

	[Sync( SyncFlags.FromHost )] public string Village { get; set; }
	[Sync( SyncFlags.FromHost )] public string Grade { get; set; }
	[Sync( SyncFlags.FromHost )] public string Nature { get; set; }
	[Sync( SyncFlags.FromHost )] public string Path { get; set; }

	/// <summary>Clan (null = sans clan) et sa validation par le staff.</summary>
	[Sync( SyncFlags.FromHost )] public string Clan { get; set; }
	[Sync( SyncFlags.FromHost )] public bool ClanValidated { get; set; }

	/// <summary>null, "moderator", "staff" ou "admin".</summary>
	[Sync( SyncFlags.FromHost )] public string StaffRank { get; set; }

	/// <summary>Médic-nin : peut relever les ninjas à terre. Validé par le staff, chargé depuis la base.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsMedic { get; set; }

	[Sync( SyncFlags.FromHost )] public long Ryo { get; set; }

	/// <summary>Temps de jeu total en secondes sur ce personnage.</summary>
	[Sync( SyncFlags.FromHost )] public int PlayTimeSeconds { get; set; }

	/// <summary>Niveau Shinobi basé sur le temps de jeu actif. Sans plafond.</summary>
	[Sync( SyncFlags.FromHost )] public int Level { get; set; } = 1;

	/// <summary>Un point de compétence par niveau gagné : c'est tout ce que rapporte un niveau.</summary>
	// shortcut: rien ne dépense encore les points, donc pas de compteur en base ; à sauvegarder le jour où on peut les dépenser.
	public int SkillPoints => Level - 1;

	/// <summary>Le joueur de ce client, ou null.</summary>
	public static NinjaPlayer Local
	{
		get
		{
			if ( !_local.IsValid() || !_local.Network.IsOwner )
				_local = Game.ActiveScene?.GetAllComponents<NinjaPlayer>().FirstOrDefault( x => x.Network.IsOwner );
			return _local;
		}
	}

	static NinjaPlayer _local;

	/// <summary>Un menu (panel staff...) a le clavier : pas de jutsu ni de défense. Client local seulement.</summary>
	public static bool InputBlocked => _openMenus.Count > 0;

	static readonly HashSet<string> _openMenus = new();

	/// <summary>Chaque menu dit s'il est ouvert : plusieurs menus ne s'écrasent plus l'un l'autre.</summary>
	public static void SetMenuOpen( string menu, bool open )
	{
		if ( open ) _openMenus.Add( menu );
		else _openMenus.Remove( menu );
	}

	TimeSince _secondTimer;

	protected override void OnAwake()
	{
		Components.GetOrCreate<NinjaKunai>();
		Components.GetOrCreate<NinjaInventory>();
		Components.GetOrCreate<NarutoRp.Combat.NinjaConsumables>();
		Components.GetOrCreate<NarutoRp.Combat.NinjaShuriken>();
		Components.GetOrCreate<NarutoRp.Missions.MissionTracker>();
		_secondTimer = 0f;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Timer chaque seconde : joueur actif ayant un personnage chargé
		while ( _secondTimer >= 1f )
		{
			_secondTimer -= 1f;

			if ( CharacterId <= 0 ) continue;

			PlayTimeSeconds++;
			var targetLevel = CalculateLevel( PlayTimeSeconds );
			if ( targetLevel > Level )
			{
				Level = targetLevel;
				NotifyLevelUpSoundRpc();
				GameManager.Current?.SaveCharacter( this );
				GameManager.Current?.LogEvent( "level_up", SteamId, null, new { level = Level, playTime = PlayTimeSeconds } );
				Log.Info( $"[Progression] {CharacterName} ({SteamId}) monte au niveau {Level} ({SkillPoints} point(s) de compétence)." );
			}
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void NotifyLevelUpSoundRpc()
	{
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/ui/coin.sound" )
			?? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_flesh.sound" );
		if ( snd is not null )
			Sound.Play( snd, WorldPosition );
	}

	/// <summary>
	/// Temps requis en secondes pour atteindre un niveau donné.
	/// Progression progressive : Niv 1 = 0 min, Niv 2 = 5 min, Niv 3 = 15 min, Niv 4 = 30 min, Niv 5 = 50 min, Niv 6 = 75 min...
	/// Formule : 5 * (L - 1) * L / 2 minutes
	/// </summary>
	public static int GetRequiredPlayTimeSeconds( int level )
	{
		if ( level <= 1 ) return 0;
		var minutes = 5 * (level - 1) * level / 2;
		return minutes * 60;
	}

	/// <summary>
	/// Calcule le niveau en fonction du temps de jeu en secondes.
	/// </summary>
	public static int CalculateLevel( int playTimeSeconds )
	{
		if ( playTimeSeconds <= 0 ) return 1;
		var minutes = playTimeSeconds / 60;
		var level = 1;
		while ( 5 * level * (level + 1) / 2 <= minutes )
		{
			level++;
		}
		return level;
	}

	/// <summary>
	/// Ratio de progression vers le prochain niveau (entre 0.0 et 1.0).
	/// </summary>
	public float LevelProgress
	{
		get
		{
			var currentSec = GetRequiredPlayTimeSeconds( Level );
			var nextSec = GetRequiredPlayTimeSeconds( Level + 1 );
			var span = nextSec - currentSec;
			if ( span <= 0 ) return 1f;
			var elapsed = Math.Max( 0, PlayTimeSeconds - currentSec );
			return Math.Clamp( (float)elapsed / span, 0f, 1f );
		}
	}

	/// <summary>
	/// Secondes restantes avant d'atteindre le niveau suivant.
	/// </summary>
	public int SecondsToNextLevel => Math.Max( 0, GetRequiredPlayTimeSeconds( Level + 1 ) - PlayTimeSeconds );

	/// <summary>
	/// Formate une durée en chaîne lisible (ex: "4m 30s", "1h 12m").
	/// </summary>
	public static string FormatDuration( int totalSeconds )
	{
		if ( totalSeconds <= 0 ) return "0s";
		var hours = totalSeconds / 3600;
		var minutes = (totalSeconds % 3600) / 60;
		var seconds = totalSeconds % 60;

		if ( hours > 0 )
			return $"{hours}h {minutes:D2}m";
		if ( minutes > 0 )
			return $"{minutes}m {seconds:D2}s";
		return $"{seconds}s";
	}

	protected override void OnDestroy()
	{
		// Déconnexion (objet détruit car orphelin) ou fin de partie : dernière sauvegarde.
		if ( Networking.IsHost )
			GameManager.Current?.SaveCharacter( this );
	}
}
