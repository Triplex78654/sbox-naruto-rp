using System;
using System.Linq;
using Sandbox;
using NarutoRp.Core;
using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// Gestion des consommables tactiques ninja équipés par le joueur :
/// - Parchemins Explosifs (Kibaku Fuda) : pose mur/sol et détonation manuelle (« Katsu ! »).
/// - Bombes Fumigènes (Kemuridama) : lancer balistique, déploiement de fumée et désorientation des IA.
/// Autorité serveur absolue pour la logique, cooldowns et dégâts.
/// </summary>
public sealed class NinjaConsumables : Component
{
	[RequireComponent] NinjaStats Stats { get; set; }
	[RequireComponent] PlayerController Controller { get; set; }

	/// <summary>Nombre maximum de parchemins explosifs posés simultanément par un ninja.</summary>
	public const int MaxExplosiveTags = 3;

	/// <summary>Délai de rechargement entre deux poses de parchemins (secondes).</summary>
	public const float ExplosiveTagCooldown = 1.2f;

	/// <summary>Délai de rechargement du lancer de bombe fumigène (secondes).</summary>
	public const float SmokeBombCooldown = 7.0f;

	/// <summary>Temps restant avant de pouvoir poser un nouveau parchemin.</summary>
	[Sync( SyncFlags.FromHost )]
	public float ExplosiveTagCooldownRemaining { get; private set; }

	/// <summary>Nombre de parchemins actuellement déployés dans le monde par ce joueur.</summary>
	[Sync( SyncFlags.FromHost )]
	public int DeployedTagCount { get; private set; }

	/// <summary>Temps restant avant de pouvoir lancer une nouvelle bombe fumigène.</summary>
	[Sync( SyncFlags.FromHost )]
	public float SmokeBombCooldownRemaining { get; private set; }

	// Chronos de l'hôte : le cooldown synchronisé est aussi écrit par la prédiction du joueur-hôte, on ne peut pas s'y fier.
	TimeSince _serverLastTag = 10f;
	TimeSince _serverLastSmoke = 10f;

	SkinnedModelRenderer GetBodyRenderer()
	{
		return Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Décrémentation des cooldowns côté hôte
		if ( ExplosiveTagCooldownRemaining > 0 )
			ExplosiveTagCooldownRemaining = MathF.Max( 0, ExplosiveTagCooldownRemaining - Time.Delta );

		if ( SmokeBombCooldownRemaining > 0 )
			SmokeBombCooldownRemaining = MathF.Max( 0, SmokeBombCooldownRemaining - Time.Delta );

		// Vérification périodique du nombre de tags déployés valides
		UpdateDeployedCount();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || NinjaPlayer.InputBlocked ) return;
		if ( Stats.IsDowned || Stats.IsStunned ) return;

		// Décrémentation locale pour l'affichage réactif du HUD
		if ( !Networking.IsHost && ExplosiveTagCooldownRemaining > 0 )
			ExplosiveTagCooldownRemaining = MathF.Max( 0, ExplosiveTagCooldownRemaining - Time.Delta );

		if ( !Networking.IsHost && SmokeBombCooldownRemaining > 0 )
			SmokeBombCooldownRemaining = MathF.Max( 0, SmokeBombCooldownRemaining - Time.Delta );

		// Pas d'action de consommable si en garde
		var defense = GetComponent<NinjaDefense>();
		if ( defense?.IsGuarding == true ) return;

		// 1. Pose de Parchemin Explosif (Touche 'F' / Action "ExplosiveTag")
		if ( Input.Pressed( "ExplosiveTag" ) &&
		     ExplosiveTagCooldownRemaining <= 0 &&
		     DeployedTagCount < MaxExplosiveTags )
		{
			TryDeployExplosiveTag();
		}

		// 2. Détonation manuelle à distance (« Katsu ! ») (Touche 'T' / Action "DetonateTag")
		if ( Input.Pressed( "DetonateTag" ) && DeployedTagCount > 0 )
		{
			TriggerManualDetonation();
		}

		// 3. Lancer de Bombe Fumigène (Touche 'G' / Action "SmokeBomb")
		if ( Input.Pressed( "SmokeBomb" ) && SmokeBombCooldownRemaining <= 0 )
		{
			TryThrowSmokeBomb();
		}
	}

	void TryDeployExplosiveTag()
	{
		var inv = GetComponent<NinjaInventory>();
		if ( inv is not null && !inv.HasItem( "explosive_tag" ) )
			return;

		var eyePos = Controller.EyePosition;
		var forward = Controller.EyeAngles.Forward;

		// Raycast pour détecter le mur ou sol visé
		var tr = Scene.Trace.Ray( eyePos, eyePos + forward * ExplosiveTag.PlacementRange )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( NetConstants.TagInvisible, NetConstants.TagPlayer )
			.Run();

		if ( !tr.Hit ) return;

		// Cooldown prédictif local
		ExplosiveTagCooldownRemaining = ExplosiveTagCooldown;

		// Animation de geste
		var body = GetBodyRenderer();
		if ( body.IsValid() )
			body.Set( "b_attack", true );

		// Son local de placement
		NinjaKunai.PlaySound( "sounds/impacts/melee/melee_knife.sound", tr.HitPosition );

		// Envoi de la requête à l'hôte
		var spawnPos = tr.HitPosition + tr.Normal * 0.4f;
		RequestDeployTag( spawnPos, tr.Normal, tr.GameObject );
	}

	void TriggerManualDetonation()
	{
		// Animation de signe / invocation
		var body = GetBodyRenderer();
		if ( body.IsValid() )
			body.Set( "b_attack", true );

		// Son d'activation Katsu
		NinjaKunai.PlaySound( "sounds/box_shine.sound", Controller.EyePosition );

		RequestDetonateTags();
	}

	void TryThrowSmokeBomb()
	{
		var inv = GetComponent<NinjaInventory>();
		if ( inv is not null && !inv.HasItem( "smoke_bomb" ) )
			return;

		// Cooldown prédictif local
		SmokeBombCooldownRemaining = SmokeBombCooldown;

		// Animation de lancer
		var body = GetBodyRenderer();
		if ( body.IsValid() )
			body.Set( "b_attack", true );

		// Son de lancer
		NinjaKunai.PlaySound( "sounds/impacts/melee/melee_knife.sound", Controller.EyePosition );

		RequestThrowSmokeBomb( Controller.EyeAngles.Forward );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void RequestDeployTag( Vector3 pos, Vector3 normal, GameObject surfaceParent )
	{
		if ( Stats.IsDowned || Stats.IsStunned ) return;
		if ( !NinjaKunai.IsValidAim( pos ) || !NinjaKunai.IsValidAim( normal ) ) return;
		if ( _serverLastTag < ExplosiveTagCooldown - 0.2f ) return;

		// Vérification anti-triche distance
		var dist = (pos - Controller.EyePosition).Length;
		if ( dist > ExplosiveTag.PlacementRange * 1.6f ) return;

		// Vérification du quota maximum
		UpdateDeployedCount();
		if ( DeployedTagCount >= MaxExplosiveTags ) return;

		// Vérification du stock d'inventaire si présent
		var inv = GetComponent<NinjaInventory>();
		if ( inv is not null && !inv.ConsumeItem( "explosive_tag" ) ) return;

		_serverLastTag = 0f;
		ExplosiveTagCooldownRemaining = ExplosiveTagCooldown;
		BroadcastAttackAnim();

		// Apparition du parchemin sur l'hôte
		ExplosiveTag.Deploy( Scene, pos, normal.Normal, GameObject, surfaceParent );
		UpdateDeployedCount();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void RequestDetonateTags()
	{
		if ( Stats.IsDowned || Stats.IsStunned ) return;

		BroadcastAttackAnim();

		// Fait exploser tous les parchemins posés par ce joueur
		var count = ExplosiveTag.DetonateAllBy( GameObject );
		UpdateDeployedCount();

		if ( count > 0 )
		{
			Log.Info( $"[Kibaku Fuda] {GameObject.Name} déclenche « Katsu ! » sur {count} parchemin(s)." );
		}
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void RequestThrowSmokeBomb( Vector3 forward )
	{
		// La position de départ vient de l'hôte : le client n'envoie que sa visée.
		if ( !NinjaKunai.IsValidAim( forward ) ) return;
		var eyePos = Controller.EyePosition;
		forward = forward.Normal;
		if ( Stats.IsDowned || Stats.IsStunned ) return;
		if ( _serverLastSmoke < SmokeBombCooldown - 0.2f ) return;

		// Vérification du stock d'inventaire si présent
		var inv = GetComponent<NinjaInventory>();
		if ( inv is not null && !inv.ConsumeItem( "smoke_bomb" ) ) return;

		_serverLastSmoke = 0f;
		SmokeBombCooldownRemaining = SmokeBombCooldown;
		BroadcastAttackAnim();

		// Apparition du projectile diffusée sur tous les clients
		var startPos = eyePos + forward * 20f;
		SpawnSmokeBombRpc( startPos, forward );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void SpawnSmokeBombRpc( Vector3 startPos, Vector3 forward )
	{
		SmokeBomb.Launch( startPos, forward, GameObject );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BroadcastAttackAnim()
	{
		var body = GetBodyRenderer();
		if ( body.IsValid() )
			body.Set( "b_attack", true );
	}

	/// <summary>
	/// Notifie le composant de la détonation ou destruction d'un tag pour actualiser le compteur.
	/// </summary>
	public void NotifyTagDetonated( ExplosiveTag tag )
	{
		UpdateDeployedCount();
	}

	void UpdateDeployedCount()
	{
		if ( !Networking.IsHost ) return;

		DeployedTagCount = ExplosiveTag.DeployedTags.Count( t => t.IsValid() && t.Owner == GameObject && !t.IsDetonated );
	}
}
