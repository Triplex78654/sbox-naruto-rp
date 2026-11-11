using System;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;
using NarutoRp.Core;
using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// État comportemental de l'IA bandit / nukenin.
/// </summary>
public enum BanditState
{
	Idle,
	Patrol,
	Chasing,
	Attacking,
	Defeated
}

/// <summary>
/// IA autonome de bandit / nukenin pour le serveur Naruto RP.
/// Gère la détection des joueurs shinobi, les rondes de patrouille, la poursuite agressive,
/// les frappes de mêlée au kunai et le jet occasionnel à mi-distance.
/// Exécution garantie côté hôte sous autorité serveur stricte.
/// </summary>
public sealed class AiBanditController : Component
{
	[Property, Group( "Identité" )]
	public string DisplayName { get; set; } = "Bandit déserteur";

	/// <summary>Type d'ennemi ("bandit", "bandit_konoha", "bandit_suna", "bandit_kumo").</summary>
	[Property, Group( "Identité" ), Sync( SyncFlags.FromHost )]
	public string EnemyType { get; set; } = "bandit";

	[Property, Group( "Statistiques" )]
	public float MaxHealth { get; set; } = 90f;

	/// <summary>Santé synchronisée depuis l'hôte.</summary>
	[Sync( SyncFlags.FromHost )]
	public float Health { get; private set; }

	/// <summary>Vrai si le bandit a été vaincu / mis à terre.</summary>
	[Sync( SyncFlags.FromHost )]
	public bool IsDefeated { get; private set; }

	[Property, Group( "Combat" )]
	public float MeleeDamage { get; set; } = 12f;

	[Property, Group( "Combat" )]
	public float MeleeRange { get; set; } = 70f;

	[Property, Group( "Combat" )]
	public float MeleeCooldown { get; set; } = 1.3f;

	[Property, Group( "Combat" )]
	public float KunaiMinRange { get; set; } = 150f;

	[Property, Group( "Combat" )]
	public float KunaiMaxRange { get; set; } = 300f;

	[Property, Group( "Combat" )]
	public float KunaiDamage { get; set; } = 18f;

	[Property, Group( "Combat" )]
	public float KunaiCooldown { get; set; } = 5f;

	[Property, Group( "Déplacement" )]
	public float DetectionRadius { get; set; } = 500f;

	[Property, Group( "Déplacement" )]
	public float PatrolSpeed { get; set; } = 110f;

	[Property, Group( "Déplacement" )]
	public float ChaseSpeed { get; set; } = 230f;

	[Property, Group( "Déplacement" )]
	public float PatrolRadius { get; set; } = 250f;

	[Property, Group( "Butin" )]
	public int MinRyoDrop { get; set; } = 25;

	[Property, Group( "Butin" )]
	public int MaxRyoDrop { get; set; } = 75;

	/// <summary>Générateur de camp responsable de ce bandit.</summary>
	public BanditSpawner Spawner { get; set; }

	/// <summary>Position d'ancrage / camp d'apparition.</summary>
	[Sync( SyncFlags.FromHost )]
	public Vector3 SpawnPosition { get; set; }

	SkinnedModelRenderer _body;
	GameObject _handKunai;

	BanditState _state = BanditState.Idle;
	NinjaPlayer _target;
	Vector3 _patrolTarget;
	TimeUntil _nextPatrolDecision;
	TimeUntil _nextMeleeAttack;
	TimeUntil _nextKunaiThrow;
	TimeUntil _disorientedUntil;
	float _currentSpeed;

	/// <summary>Vrai si le bandit est désorienté ou aveuglé (ex: nuage de fumée Kemuridama).</summary>
	public bool IsDisoriented => _disorientedUntil > 0;

	/// <summary>
	/// Désoriente le bandit (bombe fumigène) : perte immédiate de cible, état d'alerte sans attaque à distance.
	/// </summary>
	public void DisorientFromSmoke( float duration = 4.5f )
	{
		if ( IsDefeated ) return;
		_target = null;
		_state = BanditState.Idle;
		_disorientedUntil = MathF.Max( _disorientedUntil.Relative, duration );
		_nextKunaiThrow = MathF.Max( _nextKunaiThrow.Relative, duration + 2.5f );
		_nextPatrolDecision = duration * 0.4f;
	}

	protected override void OnAwake()
	{
		_body = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( _body is null )
		{
			_body = Components.Create<SkinnedModelRenderer>();
			_body.Model = Model.Load( "models/citizen/citizen.vmdl" );
		}

		CreateHandKunai();

		if ( Networking.IsHost )
		{
			Health = MaxHealth;
			SpawnPosition = WorldPosition;
			_state = BanditState.Idle;
			_nextPatrolDecision = 2f;
		}
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		// Ici et pas dans OnAwake : le camp règle MaxHealth juste après avoir créé le component.
		Health = MaxHealth;
		if ( SpawnPosition == Vector3.Zero ) SpawnPosition = WorldPosition;
	}

	void CreateHandKunai()
	{
		if ( _handKunai.IsValid() || _body is null ) return;

		_handKunai = new GameObject( true, "BanditHandKunai" );
		_handKunai.Parent = _body.GameObject;
		_handKunai.NetworkMode = NetworkMode.Never;

		var mr = _handKunai.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/ninja/kunai/kunai.vmdl" );
	}

	protected override void OnUpdate()
	{
		// Mise à jour visuelle des animations et pose d'arme Citizen
		if ( _body.IsValid() )
		{
			if ( !IsDefeated )
			{
				_body.Set( "holdtype", 6 ); // melee_weapons
				_body.Set( "holdtype_handedness", 0 ); // main droite
				_body.Set( "move_groundspeed", _currentSpeed );
			}
			else
			{
				_body.Set( "holdtype", 0 );
			}
		}
	}

	protected override void OnPreRender()
	{
		UpdateHandKunaiTransform();
	}

	void UpdateHandKunaiTransform()
	{
		if ( !_handKunai.IsValid() )
		{
			CreateHandKunai();
			if ( !_handKunai.IsValid() ) return;
		}

		if ( IsDefeated || _body is null )
		{
			_handKunai.Enabled = false;
			return;
		}

		_handKunai.Enabled = true;

		if ( TryGetHandTransform( _body, out var handTx ) )
		{
			_handKunai.WorldPosition = handTx.Position + handTx.Rotation * new Vector3( 0.0f, 0f, -1.0f );
			_handKunai.WorldRotation = handTx.Rotation * Rotation.From( -90f, 0f, 0f );
		}
		else
		{
			_handKunai.WorldPosition = _body.WorldPosition + _body.WorldRotation.Right * 12f + _body.WorldRotation.Forward * 8f + Vector3.Up * 36f;
			_handKunai.WorldRotation = _body.WorldRotation;
		}
	}

	bool TryGetHandTransform( SkinnedModelRenderer body, out Transform tx )
	{
		tx = default;
		if ( body is null ) return false;

		var att = body.GetAttachment( "hold_R" ) ?? body.GetAttachment( "hand_R" );
		if ( att.HasValue && att.Value.Position != Vector3.Zero )
		{
			tx = att.Value;
			return true;
		}

		if ( body.TryGetBoneTransform( "hold_R", out var b1 ) && b1.Position != Vector3.Zero )
		{
			tx = b1;
			return true;
		}

		if ( body.TryGetBoneTransform( "hand_R", out var b2 ) && b2.Position != Vector3.Zero )
		{
			tx = b2;
			return true;
		}

		return false;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || IsDefeated ) return;

		TickStateMachine();
	}

	void TickStateMachine()
	{
		// Si le bandit est désorienté par un fumigène, il perd sa cible et reste en alerte/patrouille confuse
		if ( IsDisoriented )
		{
			_target = null;
			TickPatrol();
			return;
		}

		// Vérification de la cible en cours
		if ( _target.IsValid() )
		{
			var stats = _target.GetComponent<NinjaStats>();
			var isStaffInvisible = _target.GetComponent<Staff.StaffCommands>()?.IsInvisible == true;
			var isTaggedDowned = _target.GameObject.Tags.Has( NetConstants.TagDowned );
			var isTaggedInvisible = _target.GameObject.Tags.Has( NetConstants.TagInvisible );

			// Perte de cible si morte, déconnectée, devenue invisible ou trop éloignée
			if ( stats is null || stats.IsDowned || isStaffInvisible || isTaggedDowned || isTaggedInvisible )
			{
				_target = null;
			}
			else
			{
				var distToCamp = (WorldPosition - SpawnPosition).Length;
				var distToTarget = (WorldPosition - _target.WorldPosition).Length;

				// Leash : si le bandit s'éloigne excessivement de son camp (plus de 1200 u), il décroche
				if ( distToCamp > 1200f || distToTarget > DetectionRadius * 2f )
				{
					_target = null;
				}
			}
		}

		// Recherche d'un joueur dans le champ visuel si aucune cible active
		if ( !_target.IsValid() )
		{
			_target = FindNearestValidPlayer( DetectionRadius );
			if ( _target.IsValid() )
			{
				_state = BanditState.Chasing;
			}
		}

		// Transition d'état
		if ( _target.IsValid() )
		{
			var dist = (WorldPosition - _target.WorldPosition).Length;

			if ( dist <= MeleeRange )
			{
				_state = BanditState.Attacking;
				ExecuteMeleeAttack();
			}
			else if ( dist >= KunaiMinRange && dist <= KunaiMaxRange && _nextKunaiThrow <= 0 )
			{
				_state = BanditState.Attacking;
				ExecuteKunaiThrow();
			}
			else
			{
				_state = BanditState.Chasing;
				MoveTowards( _target.WorldPosition, ChaseSpeed );
			}
		}
		else
		{
			TickPatrol();
		}
	}

	void TickPatrol()
	{
		if ( _nextPatrolDecision <= 0 )
		{
			var rng = new Random();
			var angle = rng.NextDouble() * Math.PI * 2.0;
			var radius = rng.NextDouble() * PatrolRadius;

			var offset = new Vector3( (float)(Math.Cos( angle ) * radius), (float)(Math.Sin( angle ) * radius), 0f );
			_patrolTarget = SpawnPosition + offset;
			_state = BanditState.Patrol;
			_nextPatrolDecision = 4f + (float)rng.NextDouble() * 3f;
		}

		if ( _state == BanditState.Patrol )
		{
			var dist = (WorldPosition - _patrolTarget).WithZ( 0 ).Length;
			if ( dist > 30f )
			{
				MoveTowards( _patrolTarget, PatrolSpeed );
			}
			else
			{
				_currentSpeed = 0f;
				_state = BanditState.Idle;
			}
		}
		else
		{
			_currentSpeed = 0f;
		}
	}

	void MoveTowards( Vector3 targetPos, float speed )
	{
		var toTarget = (targetPos - WorldPosition).WithZ( 0 );
		if ( toTarget.Length < 1f )
		{
			_currentSpeed = 0f;
			return;
		}

		var moveDir = toTarget.Normal;
		_currentSpeed = speed;

		// Orientation progressive vers la cible
		var targetRot = Rotation.LookAt( moveDir, Vector3.Up );
		WorldRotation = Rotation.Slerp( WorldRotation, targetRot, Time.Delta * 8f );

		var deltaStep = moveDir * speed * Time.Delta;
		var nextPos = WorldPosition + deltaStep;

		// Collision horizontale : évite de traverser les murs
		var wallTrace = Scene.Trace.Ray( WorldPosition + Vector3.Up * 32f, nextPos + Vector3.Up * 32f )
			.Radius( 14f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( NetConstants.TagInvisible, NetConstants.TagPlayer )
			.Run();

		if ( wallTrace.Hit )
		{
			// Glisse le long de l'obstacle
			var slideDir = Vector3.VectorPlaneProject( moveDir, wallTrace.Normal ).Normal;
			nextPos = WorldPosition + slideDir * speed * Time.Delta * 0.5f;
		}

		// Trace verticale pour coller au relief du terrain
		var groundTrace = Scene.Trace.Ray( nextPos + Vector3.Up * 48f, nextPos - Vector3.Up * 128f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( NetConstants.TagInvisible, NetConstants.TagPlayer )
			.Run();

		if ( groundTrace.Hit && !groundTrace.StartedSolid )
		{
			nextPos = groundTrace.HitPosition;
		}

		WorldPosition = nextPos;
	}

	void ExecuteMeleeAttack()
	{
		_currentSpeed = 0f;

		// Rotation vers la cible
		if ( _target.IsValid() )
		{
			var dir = (_target.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
			if ( dir.Length > 0.01f )
				WorldRotation = Rotation.LookAt( dir, Vector3.Up );
		}

		if ( _nextMeleeAttack > 0 ) return;
		_nextMeleeAttack = MeleeCooldown;

		// Déclenche l'animation de frappe
		BroadcastAttackAnim();

		// Joue le son de frappe
		PlayMeleeSound();

		// Inflige les dégâts au ninja
		var stats = _target?.GetComponent<NinjaStats>();
		if ( stats.IsValid() && !stats.IsDowned )
		{
			stats.TakeDamage( MeleeDamage, GameObject );
		}
	}

	void ExecuteKunaiThrow()
	{
		_currentSpeed = 0f;

		if ( !_target.IsValid() ) return;

		var dir = (_target.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
		if ( dir.Length > 0.01f )
			WorldRotation = Rotation.LookAt( dir, Vector3.Up );

		_nextKunaiThrow = KunaiCooldown;

		// Animation de lancer
		BroadcastAttackAnim();

		// Son de lancer
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/melee_knife.sound" );
		if ( snd is not null )
			Sound.Play( snd, WorldPosition + Vector3.Up * 45f );

		// Apparition du projectile
		var startPos = WorldPosition + Vector3.Up * 48f + WorldRotation.Forward * 20f;
		var targetPos = _target.WorldPosition + Vector3.Up * 40f;
		SpawnThrownKunaiRpc( startPos, (targetPos - startPos).Normal );
	}

	/// <summary>Kunai du bandit, simulé chez chaque joueur ; seul l'hôte applique les dégâts.</summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	void SpawnThrownKunaiRpc( Vector3 startPos, Vector3 aimDir )
	{
		var kunaiObj = new GameObject( true, "BanditThrownKunai" ) { NetworkMode = NetworkMode.Never };
		kunaiObj.WorldPosition = startPos;
		kunaiObj.WorldRotation = Rotation.LookAt( aimDir ) * Rotation.From( 90f, 0f, 0f );

		var mr = kunaiObj.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/ninja/kunai/kunai.vmdl" );

		var proj = kunaiObj.Components.Create<KunaiProjectile>();
		proj.Initialize( aimDir * 1800f, KunaiDamage, GameObject );
	}

	/// <summary>
	/// Réception de dégâts par le bandit. Exécuté sur l'hôte.
	/// </summary>
	public void TakeDamage( float amount, GameObject attacker = null )
	{
		if ( !Networking.IsHost || IsDefeated ) return;
		if ( !float.IsFinite( amount ) || amount <= 0f ) return;

		Health = MathF.Max( 0f, Health - amount );

		// Son d'impact sur chair
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_flesh.sound" );
		if ( snd is not null )
			Sound.Play( snd, WorldPosition + Vector3.Up * 40f );

		// Aggro immédiat sur l'agresseur, même hors du rayon classique
		if ( attacker.IsValid() )
		{
			var ninja = attacker.Components.GetInAncestorsOrSelf<NinjaPlayer>();
			if ( ninja.IsValid() )
			{
				_target = ninja;
				_state = BanditState.Chasing;
			}
		}

		if ( Health <= 0f )
		{
			OnDefeated( attacker );
		}
	}

	void OnDefeated( GameObject attacker )
	{
		if ( IsDefeated ) return;
		IsDefeated = true;
		_state = BanditState.Defeated;
		_currentSpeed = 0f;
		_target = null;

		Log.Info( $"[Bandit] {DisplayName} a été vaincu par {attacker?.Name ?? "inconnu"}." );

		// Déclenche l'animation de défaite
		BroadcastDefeatedAnim();

		// Notification pour les quêtes et missions du joueur vainqueur
		attacker?.Components.GetInAncestorsOrSelf<NarutoRp.Missions.MissionTracker>()?.NotifyEnemyDefeated( EnemyType ?? "bandit" );

		// Notification du camp d'origine pour enclencher le respawn
		Spawner?.OnBanditDied( this );

		// Apparition du butin en Ryô
		LootDrop.Spawn( Scene, WorldPosition + Vector3.Up * 10f, MinRyoDrop, MaxRyoDrop );

		// Destruction du corps après un délai de 5 secondes
		_ = DestroyDelayed( 5f );
	}

	async Task DestroyDelayed( float delaySeconds )
	{
		await Task.Delay( (int)(delaySeconds * 1000f) );
		if ( GameObject.IsValid() )
		{
			GameObject.Destroy();
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BroadcastAttackAnim()
	{
		if ( _body.IsValid() )
		{
			_body.Set( "b_attack", true );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BroadcastDefeatedAnim()
	{
		if ( _body.IsValid() )
		{
			_body.Set( "b_die", true );
			_body.Set( "b_downed", true );
		}

		if ( _handKunai.IsValid() )
		{
			_handKunai.Enabled = false;
		}
	}

	void PlayMeleeSound()
	{
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_flesh.sound" )
			?? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/melee_knife.sound" );

		if ( snd is not null )
		{
			Sound.Play( snd, WorldPosition + Vector3.Up * 40f );
		}
	}

	NinjaPlayer FindNearestValidPlayer( float maxRadius )
	{
		if ( IsDisoriented ) return null;

		NinjaPlayer best = null;
		var bestDistSq = maxRadius * maxRadius;

		foreach ( var player in Scene.GetAllComponents<NinjaPlayer>() )
		{
			if ( !player.IsValid() || player.CharacterId <= 0 ) continue;

			var stats = player.GetComponent<NinjaStats>();
			if ( stats is null || stats.IsDowned ) continue;
			if ( player.GameObject.Tags.Has( NetConstants.TagDowned ) ) continue;

			if ( player.GameObject.Tags.Has( NetConstants.TagInvisible ) ) continue;
			if ( player.GetComponent<Staff.StaffCommands>()?.IsInvisible == true ) continue;

			var distSq = (player.WorldPosition - WorldPosition).LengthSquared;
			if ( distSq > bestDistSq ) continue;

			// Rayon de visibilité / Ligne de mire
			var eyePos = WorldPosition + Vector3.Up * 54f;
			var targetEye = player.WorldPosition + Vector3.Up * 54f;

			// Nuage de fumée obstruant la visibilité
			if ( SmokeCloud.IsLineObscured( eyePos, targetEye ) )
				continue;

			var tr = Scene.Trace.Ray( eyePos, targetEye )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( NetConstants.TagInvisible, NetConstants.TagPlayer )
				.Run();

			if ( tr.Hit && tr.Distance < (targetEye - eyePos).Length - 20f )
				continue; // Bloqué par le décor

			bestDistSq = distSq;
			best = player;
		}

		return best;
	}

	protected override void OnDestroy()
	{
		if ( _handKunai.IsValid() )
		{
			_handKunai.Destroy();
		}
	}
}
