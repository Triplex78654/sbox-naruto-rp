using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using NarutoRp.Core;
using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// Bombe Fumigène ninja (Kemuridama).
/// Projectile lancé à la main suivant une trajectoire balistique.
/// À l'impact, déploie un nuage de fumée dense et volumineux (rayon ~200 u) pendant ~6.5 secondes.
/// Fait perdre leur cible aux bandits (AiBanditController) et bloque la ligne de vue.
/// </summary>
public sealed class SmokeBomb : Component
{
	/// <summary>Vitesse initiale du projectile lancé (unités/s).</summary>
	public const float ThrowSpeed = 1400f;

	/// <summary>Durée de maintien du nuage de fumée (secondes).</summary>
	public const float CloudDuration = 6.5f;

	/// <summary>Rayon d'action et d'opacité du nuage de fumée (unités).</summary>
	public const float CloudRadius = 200f;

	/// <summary>
	/// Fait apparaître et lance une bombe fumigène depuis la position et direction spécifiées.
	/// </summary>
	public static void Launch( Vector3 startPos, Vector3 forward, GameObject owner )
	{
		var velocity = forward * ThrowSpeed + Vector3.Up * 160f;

		var bombObj = new GameObject( true, "SmokeBombProjectile" ) { NetworkMode = NetworkMode.Never };
		bombObj.WorldPosition = startPos;
		bombObj.WorldRotation = Rotation.LookAt( velocity.Normal );

		var proj = bombObj.Components.Create<SmokeBombProjectile>();
		proj.Initialize( velocity, owner );
	}
}

/// <summary>
/// Logique balistique et détection d'impact du projectile de la bombe fumigène en vol.
/// </summary>
public sealed class SmokeBombProjectile : Component
{
	Vector3 _velocity;
	GameObject _owner;
	TimeSince _aliveTime;
	TrailRenderer _trail;
	ModelRenderer _model;
	bool _hasImpacted;

	public void Initialize( Vector3 velocity, GameObject owner )
	{
		_velocity = velocity;
		_owner = owner;
		_aliveTime = 0;
		_hasImpacted = false;

		// Modèle visuel de la bille / sphère noire
		_model = Components.Create<ModelRenderer>();
		_model.Model = Model.Load( "models/dev/sphere.vmdl" );
		_model.Tint = new Color( 0.18f, 0.18f, 0.20f );
		WorldScale = 0.22f;

		CreateTrail();

		// Son de lancer fendant l'air
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/melee_knife.sound" );
		if ( snd is not null )
			Sound.Play( snd, WorldPosition );
	}

	void CreateTrail()
	{
		var trailGo = new GameObject( GameObject, true, "SmokeTrail" ) { NetworkMode = NetworkMode.Never };
		trailGo.LocalPosition = Vector3.Zero;

		_trail = trailGo.Components.Create<TrailRenderer>();
		_trail.LifeTime = 0.25f;
		_trail.PointDistance = 5f;
		_trail.MaxPoints = 28;
		_trail.Opaque = false;
		_trail.BlendMode = BlendMode.Lighten;

		Curve w = 3.5f;
		w.AddPoint( 0f, 3.5f );
		w.AddPoint( 1f, 0f );
		_trail.Width = w;

		Gradient g = new Color( 0.75f, 0.75f, 0.8f, 0.45f );
		g.AddAlpha( 0f, 0.45f );
		g.AddAlpha( 1f, 0f );
		_trail.Color = g;
	}

	protected override void OnUpdate()
	{
		if ( _hasImpacted ) return;

		// Sécurité si lancée dans le vide sans jamais toucher d'obstacle
		if ( _aliveTime > 6f )
		{
			GameObject.Destroy();
			return;
		}

		// Gravité balistique (arc naturel)
		_velocity += Vector3.Down * 420f * Time.Delta;

		var nextPos = WorldPosition + _velocity * Time.Delta;
		var trace = Scene.Trace.Ray( WorldPosition, nextPos )
			.Radius( 3.5f );

		if ( _owner.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _owner );

		var tr = trace.Run();

		if ( tr.Hit )
		{
			_hasImpacted = true;
			WorldPosition = tr.HitPosition;

			// Déploiement du nuage à l'impact
			OnImpact( tr.HitPosition + tr.Normal * 6f );
		}
		else
		{
			WorldPosition = nextPos;
			if ( _velocity.LengthSquared > 0.01f )
			{
				WorldRotation = Rotation.LookAt( _velocity.Normal );
			}
		}
	}

	void OnImpact( Vector3 impactPoint )
	{
		// 1. Déploiement du nuage de fumée sur l'hôte (logique et désorientation IA)
		if ( Networking.IsHost )
		{
			SmokeCloud.Spawn( Scene, impactPoint, SmokeBomb.CloudRadius, SmokeBomb.CloudDuration );
		}

		// 2. Diffusion des effets visuels et sonores d'impact
		BroadcastImpactFxRpc( impactPoint );

		// 3. Destruction du projectile
		GameObject.Destroy();
	}

	void BroadcastImpactFxRpc( Vector3 impactPoint )
	{
		// Claquement sec à l'impact
		var popSound = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-generic.sound" )
			?? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_world.sound" );

		if ( popSound is not null )
			Sound.Play( popSound, impactPoint );

		// Sifflement de déploiement de la fumée
		var hissSound = ResourceLibrary.Get<SoundEvent>( "sounds/box_shine.sound" );
		if ( hissSound is not null )
			Sound.Play( hissSound, impactPoint );
	}
}

/// <summary>
/// Nuage de fumée volumineux déployé au sol.
/// Bloque les lignes de visée des bandits IA et les désoriente en continu.
/// </summary>
public sealed class SmokeCloud : Component
{
	/// <summary>Registre statique de tous les nuages de fumée actuellement actifs dans la scène.</summary>
	public static readonly List<SmokeCloud> ActiveClouds = new();

	/// <summary>Rayon effectif du nuage (unités).</summary>
	[Property]
	public float CloudRadius { get; set; } = 200f;

	/// <summary>Durée de vie totale avant dissipation (secondes).</summary>
	[Property]
	public float LifeDuration { get; set; } = 6.5f;

	TimeSince _timeSinceCreated;
	TimeUntil _nextBanditPulse;
	bool _isReleasing;
	GameObject _fxRoot;

	protected override void OnAwake()
	{
		_timeSinceCreated = 0;
	}

	protected override void OnStart()
	{
		ActiveClouds.Add( this );

		// Création du rendu visuel de fumée volumineuse
		BuildVisualCloud();
	}

	void BuildVisualCloud()
	{
		_fxRoot = JutsuFxKit.Root( "KemuridamaCloudFx", WorldPosition, LifeDuration + 1.5f );

		// Couche 1 : Coeur de fumée volumineuse dense (particules s'étendant à ~200 u)
		var coreSmoke = JutsuFxKit.Layer(
			_fxRoot,
			"textures/particles/particle_noisesphere_color.vtex",
			additive: false,
			max: 42,
			radius: 65f,
			burst: 20f,
			rate: 10f,
			duration: LifeDuration - 1.2f,
			loop: true
		);
		coreSmoke.Lifetime = new ParticleFloat( 2.2f, 3.4f );
		coreSmoke.Scale = JutsuFxKit.OverLife( 50f, CloudRadius * 1.05f );
		coreSmoke.Gradient = JutsuFxKit.ColorOverLife( new Color( 0.76f, 0.76f, 0.78f ), new Color( 0.44f, 0.44f, 0.46f ) );
		coreSmoke.Alpha = JutsuFxKit.OverLife( 0.85f, 0f );
		coreSmoke.Force = true;
		coreSmoke.ForceDirection = Vector3.Up * 15f;
		coreSmoke.Damping = 1.3f;

		// Couche 2 : Volutes vaporeuses sur le pourtour
		var wispySmoke = JutsuFxKit.Layer(
			_fxRoot,
			"textures/particles/smoke/smoke_wispy_burst.vtex",
			additive: false,
			max: 26,
			radius: 75f,
			burst: 12f,
			rate: 7f,
			duration: LifeDuration - 1.2f,
			loop: true
		);
		wispySmoke.Lifetime = new ParticleFloat( 2.5f, 3.8f );
		wispySmoke.Scale = JutsuFxKit.OverLife( 65f, CloudRadius * 1.15f );
		wispySmoke.Gradient = new Color( 0.68f, 0.68f, 0.70f );
		wispySmoke.Alpha = JutsuFxKit.OverLife( 0.60f, 0f );
		wispySmoke.Force = true;
		wispySmoke.ForceDirection = Vector3.Up * 22f;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Début d'estompage progressif avant disparition
		if ( !_isReleasing && _timeSinceCreated >= LifeDuration - 1.5f )
		{
			_isReleasing = true;
			if ( _fxRoot.IsValid() )
			{
				JutsuFxKit.Release( _fxRoot, 1.8f );
			}
		}

		// Fin de vie du nuage
		if ( _timeSinceCreated >= LifeDuration )
		{
			GameObject.Destroy();
			return;
		}

		// Pulsation périodique de désorientation sur les bandits IA (toutes les 0.25s)
		if ( _nextBanditPulse <= 0 )
		{
			_nextBanditPulse = 0.25f;
			PulseDisorientBandits();
		}
	}

	void PulseDisorientBandits()
	{
		var checkRadiusSq = (CloudRadius + 40f) * (CloudRadius + 40f);

		foreach ( var bandit in Scene.GetAllComponents<AiBanditController>() )
		{
			if ( !bandit.IsValid() || bandit.IsDefeated ) continue;

			var distSq = (bandit.WorldPosition - WorldPosition).LengthSquared;
			if ( distSq <= checkRadiusSq )
			{
				// Désoriente le bandit : perte de cible, état d'alerte, blocage du jet de kunai
				bandit.DisorientFromSmoke( 4.5f );
			}
		}
	}

	/// <summary>
	/// Détermine si une position est située à l'intérieur d'un nuage de fumée actif.
	/// </summary>
	public static bool IsInsideAnyCloud( Vector3 position )
	{
		foreach ( var cloud in ActiveClouds )
		{
			if ( !cloud.IsValid() ) continue;
			if ( (position - cloud.WorldPosition).LengthSquared <= cloud.CloudRadius * cloud.CloudRadius )
				return true;
		}
		return false;
	}

	/// <summary>
	/// Vérifie si la ligne de mire entre deux points passe à travers un nuage de fumée dense.
	/// Utilisé pour bloquer la vision des bandits et des cibles IA.
	/// </summary>
	public static bool IsLineObscured( Vector3 from, Vector3 to )
	{
		foreach ( var cloud in ActiveClouds )
		{
			if ( !cloud.IsValid() ) continue;

			var center = cloud.WorldPosition;
			var radius = cloud.CloudRadius;
			var radiusSq = radius * radius;

			// Si une des extrémités est dans le nuage
			if ( (from - center).LengthSquared <= radiusSq || (to - center).LengthSquared <= radiusSq )
				return true;

			// Distance du segment de droite au centre du nuage
			var seg = to - from;
			var lenSq = seg.LengthSquared;
			if ( lenSq < 0.001f ) continue;

			var t = Math.Clamp( Vector3.Dot( center - from, seg ) / lenSq, 0f, 1f );
			var closestPoint = from + seg * t;

			if ( (closestPoint - center).LengthSquared <= radiusSq )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Fait apparaître un nuage de fumée au sol.
	/// </summary>
	public static SmokeCloud Spawn( Scene scene, Vector3 position, float radius = 200f, float duration = 6.5f )
	{
		if ( scene is null ) return null;

		var cloudObj = new GameObject( true, "SmokeCloud" );
		cloudObj.WorldPosition = position;

		var cloud = cloudObj.Components.Create<SmokeCloud>();
		cloud.CloudRadius = radius;
		cloud.LifeDuration = duration;

		// Objet réseau : la fumée est construite dans OnStart, donc chez chaque joueur.
		cloudObj.NetworkSpawn( new NetworkSpawnOptions { OwnerTransfer = OwnerTransfer.Fixed } );

		return cloud;
	}

	protected override void OnDestroy()
	{
		ActiveClouds.Remove( this );

		if ( _fxRoot.IsValid() )
		{
			_fxRoot.Destroy();
		}
	}
}
