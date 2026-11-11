using NarutoRp.Core;
using NarutoRp.Player;

namespace NarutoRp.Missions;

/// <summary>
/// Gestionnaire du suivi de mission attaché au joueur ninja.
/// Seul l'hôte modifie l'état de la mission en cours. Les données sont répliquées
/// vers les clients via [Sync( SyncFlags.FromHost )].
/// </summary>
public sealed class MissionTracker : Component
{
	[RequireComponent] NinjaPlayer Ninja { get; set; }

	/// <summary>Identifiant de la mission active (vide = aucune mission en cours).</summary>
	[Sync( SyncFlags.FromHost )] public string ActiveMissionId { get; set; } = "";

	/// <summary>Titre de la mission active synchronisé pour l'affichage HUD.</summary>
	[Sync( SyncFlags.FromHost )] public string ActiveMissionTitle { get; set; } = "";

	/// <summary>Rang de la mission active.</summary>
	[Sync( SyncFlags.FromHost )] public MissionRank ActiveMissionRank { get; set; } = MissionRank.D;

	/// <summary>Index de l'étape courante dans la liste des objectifs.</summary>
	[Sync( SyncFlags.FromHost )] public int CurrentObjectiveIndex { get; set; } = 0;

	/// <summary>Progression numérique actuelle de l'étape en cours.</summary>
	[Sync( SyncFlags.FromHost )] public int CurrentProgress { get; set; } = 0;

	/// <summary>Objectif numérique total à atteindre pour cette étape.</summary>
	[Sync( SyncFlags.FromHost )] public int TargetCount { get; set; } = 0;

	/// <summary>Coordonnées cibles de l'étape en cours dans le monde.</summary>
	[Sync( SyncFlags.FromHost )] public Vector3 TargetLocation { get; set; } = Vector3.Zero;

	/// <summary>Description de l'objectif courant affichée sur le HUD.</summary>
	[Sync( SyncFlags.FromHost )] public string ObjectiveDescription { get; set; } = "";

	/// <summary>Délai entre la fin d'une mission et la suivante (secondes).</summary>
	// shortcut: le délai repart de zéro à la reconnexion, à sauvegarder en base si des joueurs en abusent.
	public const float MissionCooldownSeconds = 600f;

	TimeSince _sinceLastCompleted = MissionCooldownSeconds;

	/// <summary>Indique si le joueur a une mission en cours.</summary>
	public bool HasActiveMission => !string.IsNullOrEmpty( ActiveMissionId );

	/// <summary>Ressource GameResource de la mission active.</summary>
	public MissionDefinition ActiveMission => string.IsNullOrEmpty( ActiveMissionId ) ? null
		: ResourceLibrary.GetAll<MissionDefinition>().FirstOrDefault( m => m.Id == ActiveMissionId );

	// ─── RPCs Client -> Hôte ──────────────────────────────────────────

	/// <summary>
	/// Demande d'acceptation d'un contrat de mission shinobi.
	/// Validée strictement sur l'hôte (grade, village, validité de la ressource).
	/// </summary>
	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestAcceptMission( string missionId )
	{
		if ( !Networking.IsHost ) return;
		if ( Rpc.Caller != Network.Owner ) return;

		var ninja = Ninja ?? GetComponent<NinjaPlayer>();
		if ( ninja is null || ninja.CharacterId <= 0 )
		{
			Log.Warning( "[Missions] Impossible d'accepter une mission sans personnage chargé." );
			return;
		}

		// On ne prend une mission qu'au bureau des missions, et pas à la chaîne.
		var nearBoard = Scene.GetAllComponents<MissionBoardTrigger>()
			.Any( b => b.WorldPosition.Distance( WorldPosition ) <= b.InteractionDistance * 1.5f );
		if ( !nearBoard || HasActiveMission || _sinceLastCompleted < MissionCooldownSeconds ) return;

		var mission = ResourceLibrary.GetAll<MissionDefinition>().FirstOrDefault( m => m.Id == missionId );
		if ( mission is null )
		{
			Log.Warning( $"[Missions] Mission inconnue : « {missionId} »." );
			return;
		}

		// Vérification du village
		if ( !MissionDefinition.IsVillageCompatible( ninja.Village, mission.Village ) )
		{
			Log.Warning( $"[Missions] {ninja.CharacterName} ({ninja.Village}) ne peut pas accepter une mission de {mission.Village}." );
			return;
		}

		// Vérification du grade
		if ( !MissionDefinition.SatisfiesGrade( ninja.Grade, mission.MinGrade ) )
		{
			Log.Warning( $"[Missions] Grade insuffisant pour {ninja.CharacterName} : requis {mission.MinGrade}, actuel {ninja.Grade}." );
			return;
		}

		// Initialisation de la mission
		ActiveMissionId = mission.Id;
		ActiveMissionTitle = mission.Title;
		ActiveMissionRank = mission.Rank;
		CurrentObjectiveIndex = 0;

		SetupObjective( mission, 0 );

		Log.Info( $"[Missions] {ninja.CharacterName} a accepté la mission « {mission.Title} » [Rang {mission.Rank}]." );

		GameManager.Current?.LogEvent( "mission_accepted", ninja.SteamId, null, new
		{
			missionId = mission.Id,
			title = mission.Title,
			rank = mission.Rank.ToString(),
			reward = mission.RewardRyo
		} );

		NotifyAcceptSoundRpc();
	}

	/// <summary>
	/// Demande d'abandon de la mission active par le joueur propriétaire.
	/// </summary>
	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestAbandonMission()
	{
		if ( !Networking.IsHost ) return;
		if ( Rpc.Caller != Network.Owner ) return;

		if ( !HasActiveMission ) return;

		var ninja = Ninja ?? GetComponent<NinjaPlayer>();
		Log.Info( $"[Missions] {ninja?.CharacterName} a abandonné la mission « {ActiveMissionTitle} »." );

		ResetMission();
	}

	// ─── Événements de Gameplay (Hôte Seulement) ───────────────────────

	/// <summary>
	/// Signalement par une IA ou le système de combat de la mort d'un ennemi vaincu par ce joueur.
	/// </summary>
	public void NotifyEnemyDefeated( string enemyTag )
	{
		if ( !Networking.IsHost || !HasActiveMission ) return;

		var mission = ActiveMission;
		if ( mission is null || CurrentObjectiveIndex >= mission.Objectives.Count ) return;

		var objective = mission.Objectives[CurrentObjectiveIndex];
		if ( objective.Type != MissionObjectiveType.DefeatEnemies ) return;

		// Vérifie le tag si spécifié
		if ( !string.IsNullOrEmpty( objective.TargetTag ) &&
		     !string.IsNullOrEmpty( enemyTag ) &&
		     !objective.TargetTag.Equals( enemyTag, StringComparison.OrdinalIgnoreCase ) )
			return;

		CurrentProgress++;

		if ( CurrentProgress >= TargetCount )
			AdvanceObjective( mission );
	}

	/// <summary>
	/// Signalement d'une interaction réussie avec un prop ou mécanisme de quête.
	/// </summary>
	public void NotifyInteractProp( string propTag )
	{
		if ( !Networking.IsHost || !HasActiveMission ) return;

		var mission = ActiveMission;
		if ( mission is null || CurrentObjectiveIndex >= mission.Objectives.Count ) return;

		var objective = mission.Objectives[CurrentObjectiveIndex];
		if ( objective.Type != MissionObjectiveType.InteractProp ) return;

		if ( !string.IsNullOrEmpty( objective.TargetTag ) &&
		     !string.IsNullOrEmpty( propTag ) &&
		     !objective.TargetTag.Equals( propTag, StringComparison.OrdinalIgnoreCase ) )
			return;

		CurrentProgress++;

		if ( CurrentProgress >= TargetCount )
			AdvanceObjective( mission );
	}

	/// <summary>
	/// Signalement de la livraison d'un objet de mission.
	/// </summary>
	public void NotifyDeliverItem( string itemTag )
	{
		if ( !Networking.IsHost || !HasActiveMission ) return;

		var mission = ActiveMission;
		if ( mission is null || CurrentObjectiveIndex >= mission.Objectives.Count ) return;

		var objective = mission.Objectives[CurrentObjectiveIndex];
		if ( objective.Type != MissionObjectiveType.DeliverItem ) return;

		if ( !string.IsNullOrEmpty( objective.TargetTag ) &&
		     !string.IsNullOrEmpty( itemTag ) &&
		     !objective.TargetTag.Equals( itemTag, StringComparison.OrdinalIgnoreCase ) )
			return;

		CurrentProgress++;

		if ( CurrentProgress >= TargetCount )
			AdvanceObjective( mission );
	}

	protected override void OnFixedUpdate()
	{
		// Vérification continue de proximité pour les objectifs de patrouille (Hôte seulement)
		if ( !Networking.IsHost || !HasActiveMission ) return;

		var mission = ActiveMission;
		if ( mission is null || CurrentObjectiveIndex >= mission.Objectives.Count ) return;

		var objective = mission.Objectives[CurrentObjectiveIndex];
		if ( objective.Type == MissionObjectiveType.PatrolPoint )
		{
			var radius = objective.TargetRadius > 0 ? objective.TargetRadius : 250f;
			var dist = Vector3.DistanceBetween( WorldPosition, objective.TargetLocation );

			if ( dist <= radius )
			{
				CurrentProgress++;
				AdvanceObjective( mission );
			}
		}
	}

	// ─── Gestion interne des étapes ───────────────────────────────────

	void SetupObjective( MissionDefinition mission, int index )
	{
		if ( mission is null || index >= mission.Objectives.Count ) return;

		var obj = mission.Objectives[index];
		CurrentProgress = 0;
		TargetCount = Math.Max( 1, obj.TargetCount );
		TargetLocation = obj.TargetLocation;
		ObjectiveDescription = obj.Description ?? "";
	}

	void AdvanceObjective( MissionDefinition mission )
	{
		var nextIndex = CurrentObjectiveIndex + 1;
		if ( nextIndex < mission.Objectives.Count )
		{
			CurrentObjectiveIndex = nextIndex;
			SetupObjective( mission, nextIndex );
			Log.Info( $"[Missions] Étape suivante pour {Ninja?.CharacterName} : {ObjectiveDescription}" );
		}
		else
		{
			CompleteMission( mission );
		}
	}

	void CompleteMission( MissionDefinition mission )
	{
		var ninja = Ninja ?? GetComponent<NinjaPlayer>();
		if ( ninja is not null )
		{
			ninja.Ryo += mission.RewardRyo;
			GameManager.Current?.SaveCharacter( ninja );

			Log.Info( $"[Missions] Mission « {mission.Title} » ACCOMPLIE par {ninja.CharacterName} ! Récompense : +{mission.RewardRyo} Ryô (Nouveau solde : {ninja.Ryo} 両)." );

			GameManager.Current?.LogEvent( "mission_completed", ninja.SteamId, null, new
			{
				missionId = mission.Id,
				title = mission.Title,
				reward = mission.RewardRyo,
				rank = mission.Rank.ToString()
			} );
		}

		_sinceLastCompleted = 0f;
		NotifyCompletionSoundRpc();
		ResetMission();
	}

	void ResetMission()
	{
		ActiveMissionId = "";
		ActiveMissionTitle = "";
		ActiveMissionRank = MissionRank.D;
		CurrentObjectiveIndex = 0;
		CurrentProgress = 0;
		TargetCount = 0;
		TargetLocation = Vector3.Zero;
		ObjectiveDescription = "";
	}

	// ─── Retours sonores ──────────────────────────────────────────────

	[Rpc.Owner]
	void NotifyAcceptSoundRpc()
	{
		PlaySoundSafe( "sounds/impacts/melee/melee_knife.sound" );
	}

	[Rpc.Owner]
	void NotifyCompletionSoundRpc()
	{
		PlaySoundSafe( "sounds/impacts/melee/knife_flesh.sound" );
	}

	void PlaySoundSafe( string soundPath )
	{
		var snd = ResourceLibrary.Get<SoundEvent>( soundPath );
		if ( snd is not null )
			Sound.Play( snd, WorldPosition );
	}
}
