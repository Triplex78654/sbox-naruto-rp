using System;
using Sandbox;
using NarutoRp.Core;
using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// Gestion du lancer de shurikens en éventail : tir simultané de 3 shurikens (-10°, 0°, +10°)
/// avec rotation balistique rapide, traînée d'air et dégâts perçants (14 par shuriken).
/// Consomme les shurikens de l'inventaire sous autorité serveur absolue.
/// Raccourci par défaut : Touche 'B' (Action "ThrowShuriken").
/// </summary>
public sealed class NinjaShuriken : Component
{
	[RequireComponent] NinjaStats Stats { get; set; }
	[RequireComponent] PlayerController Controller { get; set; }

	/// <summary>Temps restant avant de pouvoir relancer une salve de shurikens (secondes).</summary>
	[Sync( SyncFlags.FromHost )]
	public float ThrowCooldownRemaining { get; private set; }

	/// <summary>Délai de rechargement entre deux salves de shurikens.</summary>
	public const float ThrowCooldown = 1.8f;

	/// <summary>Vitesse initiale de propulsion des shurikens (unités/s).</summary>
	public const float ThrowSpeed = 2300f;

	/// <summary>Dégâts par shuriken au contact.</summary>
	public const float ShurikenDamage = 14f;

	/// <summary>Nombre de shurikens propulsés par salve.</summary>
	public const int ShurikensPerThrow = 3;

	TimeSince _serverLastThrow = 10f;

	SkinnedModelRenderer GetBodyRenderer()
	{
		return Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
	}

	protected override void OnFixedUpdate()
	{
		// Décrémente le cooldown sur l'hôte pour la réplication réseau
		if ( Networking.IsHost && ThrowCooldownRemaining > 0 )
			ThrowCooldownRemaining = MathF.Max( 0, ThrowCooldownRemaining - Time.Delta );
	}

	protected override void OnUpdate()
	{
		// Prédiction locale du cooldown pour l'affichage réactif du HUD
		// L'hôte décompte déjà dans OnFixedUpdate : seul le client propriétaire prédit.
		if ( !Networking.IsHost && !IsProxy && ThrowCooldownRemaining > 0 )
			ThrowCooldownRemaining = MathF.Max( 0, ThrowCooldownRemaining - Time.Delta );

		if ( IsProxy || NinjaPlayer.InputBlocked ) return;
		if ( Stats.IsDowned || Stats.IsStunned ) return;

		// Déclenchement : touche 'B' ("ThrowShuriken")
		if ( Input.Pressed( "ThrowShuriken" ) && ThrowCooldownRemaining <= 0 )
		{
			TryThrowShurikens();
		}
	}

	void TryThrowShurikens()
	{
		var inv = GetComponent<NinjaInventory>();
		if ( inv.IsValid() && inv.ShurikenCount <= 0 )
		{
			// Pas de shuriken disponible en réserve
			return;
		}

		var defense = GetComponent<NinjaDefense>();
		if ( defense?.IsGuarding == true ) return;

		// Animation de lancer avec le bras droit
		var body = GetBodyRenderer();
		if ( body.IsValid() )
		{
			body.Set( "b_attack", true );
		}

		// Prédiction locale immédiate du cooldown
		ThrowCooldownRemaining = ThrowCooldown;

		// Envoi de la requête RPC au serveur
		RequestThrowShurikens( Controller.EyeAngles );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void RequestThrowShurikens( Angles eyeAngles )
	{
		if ( Stats.IsDowned || Stats.IsStunned || !NinjaKunai.IsValidAim( eyeAngles.Forward ) ) return;
		if ( GetComponent<NinjaDefense>()?.IsGuarding == true ) return;
		if ( _serverLastThrow < ThrowCooldown - 0.2f ) return;

		var inv = GetComponent<NinjaInventory>();
		var countToThrow = ShurikensPerThrow;

		if ( inv.IsValid() )
		{
			if ( inv.ShurikenCount <= 0 ) return;
			countToThrow = Math.Min( countToThrow, inv.ShurikenCount );
			inv.ShurikenCount -= countToThrow;
		}

		_serverLastThrow = 0f;
		ThrowCooldownRemaining = ThrowCooldown;

		// Calcul des 3 trajectoires en éventail horizontal
		// La position de départ vient de l'hôte : le client n'envoie que sa visée.
		var centerForward = eyeAngles.Forward;
		var right = eyeAngles.ToRotation().Right;
		var startPos = Controller.EyePosition + centerForward * 20f + right * 8f;

		float[] angleOffsets = countToThrow switch
		{
			1 => new[] { 0f },
			2 => new[] { -6f, 6f },
			_ => new[] { -10f, 0f, 10f }
		};

		foreach ( var offsetDeg in angleOffsets )
		{
			var spreadRot = Rotation.FromAxis( Vector3.Up, offsetDeg );
			var dir = spreadRot * centerForward;
			var velocity = dir * ThrowSpeed;

			SpawnThrownShurikenRpc( startPos, velocity, offsetDeg );
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void SpawnThrownShurikenRpc( Vector3 startPos, Vector3 velocity, float offsetDeg )
	{
		// Son de sifflement rapide de l'acier fendant l'air
		NinjaKunai.PlaySound( "sounds/impacts/melee/melee_knife.sound", startPos );

		SpawnThrownShuriken( startPos, velocity );
	}

	void SpawnThrownShuriken( Vector3 startPos, Vector3 velocity )
	{
		var shurikenObj = new GameObject( true, "ThrownShuriken" );
		shurikenObj.WorldPosition = startPos;
		shurikenObj.WorldRotation = Rotation.LookAt( velocity.Normal );
		shurikenObj.NetworkMode = NetworkMode.Never;

		var mr = shurikenObj.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/ninja/shuriken/shuriken.vmdl" );

		var projectile = shurikenObj.Components.Create<ShurikenProjectile>();
		projectile.Initialize( velocity, ShurikenDamage, GameObject );
	}
}

/// <summary>
/// Logique balistique d'un shuriken individuel en vol :
/// Rotation rapide sur son axe, traînée fine, pénétration ou fixation sur décor.
/// </summary>
public sealed class ShurikenProjectile : Component
{
	Vector3 _velocity;
	float _damage;
	GameObject _owner;
	TimeSince _aliveTime;
	TimeSince _stuckTime;
	bool _stuck;
	TrailRenderer _trail;
	float _spinRate = 1800f; // 5 tours complets par seconde

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
		var trailGo = new GameObject( GameObject, true, "ShurikenTrail" ) { NetworkMode = NetworkMode.Never };
		trailGo.LocalPosition = Vector3.Zero;

		_trail = trailGo.Components.Create<TrailRenderer>();
		_trail.LifeTime = 0.15f;
		_trail.PointDistance = 3f;
		_trail.MaxPoints = 24;
		_trail.Opaque = false;
		_trail.BlendMode = BlendMode.Lighten;

		Curve w = 1.5f;
		w.AddPoint( 0f, 1.5f );
		w.AddPoint( 1f, 0f );
		_trail.Width = w;

		Gradient g = new Color( 0.85f, 0.9f, 1f, 0.45f );
		g.AddAlpha( 0f, 0.45f );
		g.AddAlpha( 1f, 0f );
		_trail.Color = g;
	}

	protected override void OnUpdate()
	{
		if ( _stuck )
		{
			// Disparaît 4.5 secondes après s'être planté
			if ( _stuckTime > 4.5f )
				GameObject.Destroy();
			return;
		}

		if ( _aliveTime > 4.5f )
		{
			GameObject.Destroy();
			return;
		}

		// Rotation circulaire à haute vitesse sur le plan de la roue
		WorldRotation *= Rotation.FromAxis( WorldRotation.Up, _spinRate * Time.Delta );

		// Légère gravité balistique (~300 u/s²)
		_velocity += Vector3.Down * 300f * Time.Delta;

		var nextPos = WorldPosition + _velocity * Time.Delta;
		var trace = Scene.Trace.Ray( WorldPosition, nextPos )
			.Radius( 2.0f );

		if ( _owner.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _owner );

		var tr = trace.Run();

		if ( tr.Hit )
		{
			WorldPosition = tr.HitPosition;
			_stuck = true;
			_stuckTime = 0;

			if ( _trail.IsValid() )
				_trail.Emitting = false;

			if ( tr.GameObject.IsValid() )
				GameObject.SetParent( tr.GameObject );

			// Dégâts si contact avec un joueur ou un bandit
			var target = tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>();
			var bandit = tr.GameObject?.Components.GetInAncestorsOrSelf<AiBanditController>();

			if ( target.IsValid() && target.GameObject != _owner && !target.IsDowned )
			{
				if ( Networking.IsHost )
					target.TakeDamage( _damage, _owner );
				NinjaKunai.PlaySound( "sounds/impacts/melee/knife_flesh.sound", tr.HitPosition );
				GameObject.Destroy(); // Traversée/destruction sur cible vivante
			}
			else if ( bandit.IsValid() && bandit.GameObject != _owner && !bandit.IsDefeated )
			{
				if ( Networking.IsHost )
					bandit.TakeDamage( _damage, _owner );
				NinjaKunai.PlaySound( "sounds/impacts/melee/knife_flesh.sound", tr.HitPosition );
				GameObject.Destroy();
			}
			else
			{
				// Impact sur surface solide : cliquetis métallique et fichage
				NinjaKunai.PlaySound( "sounds/impacts/melee/knife_metal.sound", tr.HitPosition );
			}
		}
		else
		{
			WorldPosition = nextPos;
		}
	}
}
