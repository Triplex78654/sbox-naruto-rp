using Sandbox;
using NarutoRp.Core;
using NarutoRp.Combat;

namespace NarutoRp.Player;

/// <summary>
/// Gestion du kunai équipé : modèle en main (hold_R), attaque au corps-à-corps (Clic gauche)
/// et lancer de projectile avec cooldown (Touche R). Validé par l'hôte en réseau.
/// </summary>
public sealed class NinjaKunai : Component
{
	[RequireComponent] NinjaStats Stats { get; set; }
	[RequireComponent] PlayerController Controller { get; set; }

	/// <summary>Modèle du kunai affiché dans la main droite du joueur.</summary>
	GameObject _handKunai;

	/// <summary>Temps restant avant de pouvoir relancer un kunai (secondes).</summary>
	[Sync( SyncFlags.FromHost )]
	public float ThrowCooldownRemaining { get; private set; }

	/// <summary>Temps écoulé depuis le dernier lancer (masque le kunai en main ~0.35s).</summary>
	TimeSince _timeSinceThrow = 10f;

	/// <summary>Dernier temps où un coup de mêlée a été porté.</summary>
	TimeSince _lastMelee;

	/// <summary>Cooldown du lancer de kunai en secondes.</summary>
	public const float ThrowCooldown = 2.5f;

	/// <summary>Dégâts au corps-à-corps.</summary>
	public const float MeleeDamage = 15f;

	/// <summary>Dégâts du kunai lancé.</summary>
	public const float ThrowDamage = 25f;

	/// <summary>Portée de la frappe de mêlée (unités).</summary>
	public const float MeleeRange = 70f;

	/// <summary>Vitesse du kunai lancé (unités/s).</summary>
	public const float ThrowSpeed = 2200f;

	/// <summary>Pose « swing » de l'AnimGraph : son b_attack est un grand geste du bras droit (taillade et lancer).</summary>
	public const int HoldTypeMeleeWeapons = 6;

	/// <summary>Pose au repos, kunai dégainé. 6 = debout, bras armé ; 5 = garde poings levés mais très accroupie.</summary>
	[Property, Group( "Placement en main" )]
	public int HoldType { get; set; } = HoldTypeMeleeWeapons;

	/// <summary>Durée de la pose d'attaque après un coup ou un lancer.</summary>
	const float AttackPoseSeconds = 0.7f;

	/// <summary>Moment du geste où le kunai quitte la main (secondes après le début du lancer).</summary>
	const float ThrowReleaseSeconds = 0.22f;

	TimeSince _timeSinceAttackAnim = 10f;

	/// <summary>Indique si le kunai est dégainé en main ou rengainé (mains nues).</summary>
	[Sync( SyncFlags.FromHost )]
	public bool IsDrawn { get; set; } // rangé à l'apparition : posture normale en ville, H pour dégainer

	TimeSince _serverLastThrow = 10f;
	TimeSince _serverLastMelee = 10f;

	[Property, Group( "Placement en main" )]
	public Vector3 GripOffset { get; set; } = new Vector3( -1.0f, -1.4f, 0.0f );

	[Property, Group( "Placement en main" )]
	public Angles GripAngles { get; set; } = Angles.Zero;

	/// <summary>Une visée envoyée par un client est utilisable : ni NaN, ni infinie, ni nulle.</summary>
	public static bool IsValidAim( Vector3 v ) => float.IsFinite( v.x ) && float.IsFinite( v.y ) && float.IsFinite( v.z ) && v.Length > 0.01f;

	SkinnedModelRenderer GetBodyRenderer()
	{
		return Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
	}

	protected override void OnAwake()
	{
		CreateHandModel();
	}

	void CreateHandModel()
	{
		if ( _handKunai.IsValid() ) return;

		var body = GetBodyRenderer();
		if ( body is null ) return;

		// Crée l'objet enfant attaché au corps
		_handKunai = new GameObject( true, "KunaiHand" );
		_handKunai.Parent = body.GameObject;
		_handKunai.NetworkMode = NetworkMode.Never;

		var mr = _handKunai.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/ninja/kunai/kunai.vmdl" );

		UpdateHandKunaiTransform();
	}

	protected override void OnFixedUpdate()
	{
		// Décrémente le cooldown sur l'hôte pour la réplication réseau
		if ( Networking.IsHost && ThrowCooldownRemaining > 0 )
			ThrowCooldownRemaining = MathF.Max( 0, ThrowCooldownRemaining - Time.Delta );
	}

	protected override void OnUpdate()
	{
		var body = GetBodyRenderer();
		if ( body.IsValid() && !Stats.IsDowned )
		{
			// Si le kunai est dégainé : holdtype 6 (arme blanche, poing fermé et garde haute).
			// Si le kunai est rengainé : holdtype 0 (mains nues / libres).
			body.Set( "holdtype", !IsDrawn ? 0 : _timeSinceAttackAnim < AttackPoseSeconds ? HoldTypeMeleeWeapons : HoldType );
			body.Set( "holdtype_handedness", 0 ); // Main droite
		}

		if ( IsProxy || NinjaPlayer.InputBlocked ) return;
		if ( Stats.IsDowned || Stats.IsStunned ) return;

		// Raccourci pour rengainer / dégainer le kunai (touche H)
		if ( Input.Pressed( "ToggleWeapon" ) )
		{
			RequestToggleDrawn();
		}

		// Mise à jour locale du cooldown pour le propriétaire (HUD / réactivité prédictive)
		if ( !Networking.IsHost && ThrowCooldownRemaining > 0 )
			ThrowCooldownRemaining = MathF.Max( 0, ThrowCooldownRemaining - Time.Delta );

		// Si l'arme est rengainée (mains nues), le Clic gauche déclenche le coup de poing dans JutsuCaster
		if ( !IsDrawn ) return;

		// Si le joueur est en garde (clic droit / Attack2 réservé à NinjaDefense), pas d'attaque ni lancer
		var defense = GetComponent<NinjaDefense>();
		if ( defense?.IsGuarding == true ) return;

		// Clic gauche : Mêlée au Kunai (Taillade avec animation)
		if ( Input.Pressed( "Attack1" ) && _lastMelee > 0.45f )
		{
			_lastMelee = 0;
			TriggerMeleeAnimation( body );
			RequestMeleeAttack( Controller.EyeAngles.Forward );
		}

		// Touche R (Reload) : Lancer de kunai balistique (cooldown 2.5s)
		if ( Input.Pressed( "Reload" ) && ThrowCooldownRemaining <= 0 )
		{
			ThrowCooldownRemaining = ThrowCooldown;
			TriggerMeleeAnimation( body );
			RequestThrowKunai( Controller.EyeAngles.Forward );
		}
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestToggleDrawn()
	{
		IsDrawn = !IsDrawn;
		PlayToggleSoundRpc( IsDrawn );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void PlayToggleSoundRpc( bool drawn )
	{
		var snd = drawn
			? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/melee_knife.sound" )
			: ResourceLibrary.Get<SoundEvent>( "sounds/ui/coin.sound" );
		if ( snd is not null )
			Sound.Play( snd, WorldPosition );
	}

	void TriggerMeleeAnimation( SkinnedModelRenderer body )
	{
		if ( body.IsValid() )
		{
			// Pose d'attaque (bras droit) puis déclenchement du geste dans l'AnimGraph
			_timeSinceAttackAnim = 0f;
			body.Set( "holdtype", HoldTypeMeleeWeapons );
			body.Set( "b_attack", true );
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
			CreateHandModel();
			if ( !_handKunai.IsValid() ) return;
		}

		var body = GetBodyRenderer();
		if ( body is null ) return;

		// Si le joueur est à terre, a rangé son arme ou vient de lancer son kunai (~0.35s), on masque le modèle en main
		if ( Stats.IsDowned || !IsDrawn || _timeSinceThrow < 0.35f )
		{
			_handKunai.Enabled = false;
			return;
		}
		_handKunai.Enabled = true;

		// Recherche de l'attachement hold_R ou de l'os de la main droite
		if ( TryGetHandTransform( body, out var bone ) )
		{
			// Le manche (grip) est centré en Z=0, et la lame va vers +Z.
			// Dans le repère de l'attache hold_R, Z est l'axe du poing : sans rotation, le manche est
			// dans la main et la lame sort côté pouce. GripOffset recentre le manche entre les doigts.
			// et la poignée entourée de tissu blanc se place au creux de la paume fermée.
			_handKunai.WorldPosition = bone.Position + bone.Rotation * GripOffset;
			_handKunai.WorldRotation = bone.Rotation * Rotation.From( GripAngles );
		}
		else
		{
			// Position de secours relative au corps si l'os n'est pas encore évalué
			_handKunai.WorldPosition = body.WorldPosition + body.WorldRotation.Right * 14f + body.WorldRotation.Forward * 8f + Vector3.Up * 36f;
			_handKunai.WorldRotation = body.WorldRotation;
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

	[Rpc.Host( NetFlags.OwnerOnly )]
	void RequestMeleeAttack( Vector3 aimForward )
	{
		if ( Stats.IsDowned || Stats.IsStunned || !IsDrawn || !IsValidAim( aimForward ) ) return;
		if ( GetComponent<NinjaDefense>()?.IsGuarding == true ) return;
		if ( _serverLastMelee < 0.35f ) return;
		_serverLastMelee = 0f;
		aimForward = aimForward.Normal;

		// Joue l'animation de frappe chez tous les autres joueurs
		BroadcastAttackAnim();

		var eye = Controller.EyePosition;
		var tr = Scene.Trace.Ray( eye, eye + aimForward * MeleeRange )
			.IgnoreGameObjectHierarchy( GameObject )
			.Radius( 6f )
			.Run();

		// Son de taillade dans l'air ou impact
		if ( tr.Hit )
		{
			var targetStats = tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>();
			var bandit = tr.GameObject?.Components.GetInAncestorsOrSelf<AiBanditController>();

			if ( targetStats.IsValid() && targetStats != Stats && !targetStats.IsDowned )
			{
				targetStats.TakeDamage( MeleeDamage, GameObject );
				PlaySound( "sounds/impacts/melee/knife_flesh.sound", tr.HitPosition );
			}
			else if ( bandit.IsValid() && !bandit.IsDefeated )
			{
				bandit.TakeDamage( MeleeDamage, GameObject );
				PlaySound( "sounds/impacts/melee/knife_flesh.sound", tr.HitPosition );
			}
			else
			{
				PlaySound( "sounds/impacts/melee/knife_world.sound", tr.HitPosition );
			}
		}
		else
		{
			PlaySound( "sounds/impacts/melee/melee_knife.sound", eye );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BroadcastAttackAnim()
	{
		// Le joueur lui-même a déjà lancé le geste au moment de l'appui : le rejouer le ferait repartir de zéro.
		if ( !IsProxy ) return;
		TriggerMeleeAnimation( GetBodyRenderer() );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void RequestThrowKunai( Vector3 aimForward )
	{
		if ( Stats.IsDowned || Stats.IsStunned || !IsDrawn || !IsValidAim( aimForward ) ) return;
		if ( _serverLastThrow < ThrowCooldown - 0.2f ) return;
		aimForward = aimForward.Normal;

		var defense = GetComponent<NinjaDefense>();
		if ( defense?.IsGuarding == true ) return;

		// Un kunai lancé est un kunai en moins (ceux de la boutique servent à ça).
		var inv = GetComponent<NinjaInventory>();
		if ( inv.IsValid() && !inv.ConsumeItem( "kunai" ) ) return;

		_serverLastThrow = 0f;
		ThrowCooldownRemaining = ThrowCooldown;
		BroadcastAttackAnim();

		var startPos = Controller.EyePosition + aimForward * 20f;
		var velocity = aimForward * ThrowSpeed;

		SpawnThrownKunaiRpc( startPos, velocity );
	}

	/// <summary>
	/// Diffuse l'apparition du kunai en vol à tous les joueurs avec ses effets et sons.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	async void SpawnThrownKunaiRpc( Vector3 startPos, Vector3 velocity )
	{
		// Le kunai reste en main pendant l'armé du bras et part au moment où le bras se détend :
		// sinon il est déjà loin avant que le geste se voie.
		await Task.DelaySeconds( ThrowReleaseSeconds );
		if ( !this.IsValid() ) return;

		// Masque le kunai dans la main le temps d'en « reprendre » un (~0.35s)
		_timeSinceThrow = 0f;

		// Son de lancer fendant l'air (woosh)
		PlaySound( "sounds/impacts/melee/melee_knife.sound", startPos );

		SpawnThrownKunai( startPos, velocity );
	}

	void SpawnThrownKunai( Vector3 startPos, Vector3 velocity )
	{
		var kunaiObj = new GameObject( true, "ThrownKunai" );
		kunaiObj.WorldPosition = startPos;
		kunaiObj.WorldRotation = Rotation.LookAt( velocity.Normal ) * Rotation.From( -90f, 0f, 0f );
		kunaiObj.NetworkMode = NetworkMode.Never;

		var mr = kunaiObj.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/ninja/kunai/kunai.vmdl" );

		var projectile = kunaiObj.Components.Create<KunaiProjectile>();
		projectile.Initialize( velocity, ThrowDamage, GameObject );
	}

	public static void PlaySound( string soundPath, Vector3 pos )
	{
		var snd = ResourceLibrary.Get<SoundEvent>( soundPath );
		if ( snd is not null )
			Sound.Play( snd, pos );
	}

	protected override void OnDestroy()
	{
		if ( _handKunai.IsValid() )
			_handKunai.Destroy();
	}
}

/// <summary>
/// Logique du projectile de kunai lancé en vol, qui se plante à l'impact ou inflige des dégâts.
/// </summary>
public sealed class KunaiProjectile : Component
{
	Vector3 _velocity;
	float _damage;
	GameObject _owner;
	TimeSince _aliveTime;
	TimeSince _stuckTime;
	bool _stuck;
	TrailRenderer _trail;

	public void Initialize( Vector3 velocity, float damage, GameObject owner )
	{
		_velocity = velocity;
		_damage = damage;
		_owner = owner;
		_aliveTime = 0;
		_stuck = false;

		CreateTrail();
	}

	void CreateTrail()
	{
		var trailGo = new GameObject( GameObject, true, "KunaiTrail" ) { NetworkMode = NetworkMode.Never };
		trailGo.LocalPosition = new Vector3( 0, 0, -6f );

		_trail = trailGo.Components.Create<TrailRenderer>();
		_trail.LifeTime = 0.2f;
		_trail.PointDistance = 4f;
		_trail.MaxPoints = 32;
		_trail.Opaque = false;
		_trail.BlendMode = BlendMode.Lighten;

		Curve w = 2.0f;
		w.AddPoint( 0f, 2.0f );
		w.AddPoint( 1f, 0f );
		_trail.Width = w;

		Gradient g = new Color( 0.95f, 0.95f, 1f, 0.5f );
		g.AddAlpha( 0f, 0.5f );
		g.AddAlpha( 1f, 0f );
		_trail.Color = g;
	}

	protected override void OnUpdate()
	{
		if ( _stuck )
		{
			// Disparaît 6 secondes après s'être planté
			if ( _stuckTime > 6f )
				GameObject.Destroy();
			return;
		}

		// Disparaît après 5 secondes en vol sans impact (ex. tir vers le ciel ou le vide)
		if ( _aliveTime > 5f )
		{
			GameObject.Destroy();
			return;
		}

		// Gravité balistique légère (~380 u/s²)
		_velocity += Vector3.Down * 380f * Time.Delta;

		var nextPos = WorldPosition + _velocity * Time.Delta;
		var trace = Scene.Trace.Ray( WorldPosition, nextPos )
			.Radius( 2.5f );

		if ( _owner.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _owner );

		var tr = trace.Run();

		if ( tr.Hit )
		{
			WorldPosition = tr.HitPosition;
			_stuck = true;
			_stuckTime = 0;

			// Arrête l'émission de la traînée pour qu'elle s'estompe naturellement
			if ( _trail.IsValid() )
				_trail.Emitting = false;

			// S'attache au support (mur, sol ou cible)
			if ( tr.GameObject.IsValid() )
				GameObject.SetParent( tr.GameObject );

			// Dégâts si c'est un joueur ou un bandit
			var target = tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>();
			var bandit = tr.GameObject?.Components.GetInAncestorsOrSelf<AiBanditController>();

			if ( target.IsValid() && target.GameObject != _owner && !target.IsDowned )
			{
				if ( Networking.IsHost )
					target.TakeDamage( _damage, _owner );
				NinjaKunai.PlaySound( "sounds/impacts/melee/knife_flesh.sound", tr.HitPosition );
			}
			else if ( bandit.IsValid() && bandit.GameObject != _owner && !bandit.IsDefeated )
			{
				if ( Networking.IsHost )
					bandit.TakeDamage( _damage, _owner );
				NinjaKunai.PlaySound( "sounds/impacts/melee/knife_flesh.sound", tr.HitPosition );
			}
			else
			{
				NinjaKunai.PlaySound( "sounds/impacts/melee/knife_world.sound", tr.HitPosition );
			}
		}
		else
		{
			WorldPosition = nextPos;
			if ( _velocity.LengthSquared > 0.01f )
			{
				var targetRot = Rotation.LookAt( _velocity.Normal ) * Rotation.From( -90f, 0f, 0f );
				WorldRotation = Rotation.Slerp( WorldRotation, targetRot, Time.Delta * 25f );
			}
		}
	}
}
