using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using NarutoRp.Core;
using NarutoRp.Player;
using NarutoRp.Jutsus;

namespace NarutoRp.Combat;

/// <summary>
/// Parchemin Explosif ninja (Kibaku Fuda).
/// Peut être posé à plat sur un mur, un sol ou un obstacle.
/// Possède deux modes de déclenchement :
/// 1. Piège de proximité : s'amorce et explose si un ennemi ou bandit approche à moins de 85 u.
/// 2. Détonation manuelle à distance : déclenchée simultanément par le poseur (« Katsu ! »).
/// Inflige des dégâts de zone (Katon), projection physique et effets visuels/sonores.
/// </summary>
public sealed class ExplosiveTag : Component
{
	/// <summary>Registre de tous les parchemins explosifs actuellement déployés dans le monde.</summary>
	public static readonly List<ExplosiveTag> DeployedTags = new();

	/// <summary>Portée de pose depuis la vue du joueur (unités).</summary>
	public const float PlacementRange = 120f;

	/// <summary>Rayon de détection de proximité (unités).</summary>
	public const float ProximityRadius = 85f;

	/// <summary>Rayon du souffle de l'explosion (unités).</summary>
	public const float ExplosionRadius = 180f;

	/// <summary>Dégâts maximum au centre de l'explosion.</summary>
	public const float MaxDamage = 55f;

	/// <summary>Délai avant armement du piège de proximité (secondes).</summary>
	public const float ArmingDelay = 0.8f;

	/// <summary>Délai d'amorçage avant détonation automatique (secondes).</summary>
	public const float PrimingDuration = 0.4f;

	/// <summary>Joueur ninja ayant posé ce parchemin.</summary>
	[Sync( SyncFlags.FromHost )]
	public GameObject Owner { get; set; }

	/// <summary>Vrai si le parchemin est amorcé et sur le point d'exploser.</summary>
	[Sync( SyncFlags.FromHost )]
	public bool IsPrimed { get; private set; }

	/// <summary>Vrai si le parchemin a déjà explosé.</summary>
	public bool IsDetonated { get; private set; }

	TimeSince _timeSincePlaced;
	TimeUntil _detonationTimer;
	bool _isArmed;

	// Éléments visuels locaux
	ModelRenderer _paperRenderer;
	ModelRenderer _kanjiRenderer;
	PointLight _warningLight;

	protected override void OnAwake()
	{
		_timeSincePlaced = 0;
		BuildVisualMesh();
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			DeployedTags.Add( this );
		}
	}

	void BuildVisualMesh()
	{
		// 1. Feuille de parchemin blanche / crème posée à plat contre la surface
		var paperObj = new GameObject( GameObject, true, "TagPaper" ) { NetworkMode = NetworkMode.Never };
		_paperRenderer = paperObj.Components.Create<ModelRenderer>();
		_paperRenderer.Model = Model.Load( "models/dev/sphere.vmdl" );
		_paperRenderer.Tint = new Color( 0.94f, 0.91f, 0.82f ); // Beige papier parchemin
		paperObj.LocalScale = new Vector3( 0.16f, 0.28f, 0.015f ); // Rectangle fin aplati

		// 2. Symbole kanji / sceau noir au centre
		var kanjiObj = new GameObject( GameObject, true, "TagKanji" ) { NetworkMode = NetworkMode.Never };
		kanjiObj.LocalPosition = new Vector3( 0f, 0f, 0.012f );
		_kanjiRenderer = kanjiObj.Components.Create<ModelRenderer>();
		_kanjiRenderer.Model = Model.Load( "models/dev/sphere.vmdl" );
		_kanjiRenderer.Tint = new Color( 0.1f, 0.1f, 0.1f ); // Encre noire
		kanjiObj.LocalScale = new Vector3( 0.08f, 0.14f, 0.018f );

		// 3. Lueur d'avertissement rouge lors de l'amorçage
		_warningLight = Components.Create<PointLight>();
		_warningLight.LightColor = new Color( 1f, 0.15f, 0.1f );
		_warningLight.Radius = 60f;
		_warningLight.Enabled = false;
	}

	protected override void OnUpdate()
	{
		// Si amorcé, fait scintiller le kanji et la lueur rouge
		if ( IsPrimed && !IsDetonated )
		{
			if ( _kanjiRenderer.IsValid() )
			{
				var pulse = (MathF.Sin( Time.Now * 35f ) + 1f) * 0.5f;
				_kanjiRenderer.Tint = Color.Lerp( Color.Red, new Color( 1f, 0.8f, 0.4f ), pulse );
			}

			if ( _warningLight.IsValid() )
			{
				_warningLight.Enabled = true;
				_warningLight.Radius = 50f + MathF.Sin( Time.Now * 40f ) * 20f;
			}
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || IsDetonated ) return;

		// Délai initial avant activation du détecteur de proximité
		if ( !_isArmed )
		{
			if ( _timeSincePlaced >= ArmingDelay )
				_isArmed = true;
			return;
		}

		// Si amorcé, compte à rebours jusqu'à l'explosion
		if ( IsPrimed )
		{
			if ( _detonationTimer <= 0 )
			{
				ExecuteDetonation();
			}
			return;
		}

		// Détection de proximité (Piège)
		CheckProximityTrigger();
	}

	void CheckProximityTrigger()
	{
		// 1. Détection des joueurs ennemis
		foreach ( var player in Scene.GetAllComponents<NinjaPlayer>() )
		{
			if ( !player.IsValid() || player.GameObject == Owner ) continue;

			var stats = player.GetComponent<NinjaStats>();
			if ( stats is null || stats.IsDowned ) continue;

			// Ignore les membres du staff invisibles
			if ( player.GameObject.Tags.Has( NetConstants.TagInvisible ) ) continue;
			if ( player.GetComponent<Staff.StaffCommands>()?.IsInvisible == true ) continue;

			var dist = (player.WorldPosition - WorldPosition).Length;
			if ( dist <= ProximityRadius )
			{
				Prime( PrimingDuration );
				return;
			}
		}

		// 2. Détection des bandits / nukenins
		foreach ( var bandit in Scene.GetAllComponents<AiBanditController>() )
		{
			if ( !bandit.IsValid() || bandit.IsDefeated ) continue;

			var dist = (bandit.WorldPosition - WorldPosition).Length;
			if ( dist <= ProximityRadius )
			{
				Prime( PrimingDuration );
				return;
			}
		}
	}

	/// <summary>
	/// Amorce le parchemin : sifflement d'avertissement, kanji rougeoyant et détonation imminente.
	/// </summary>
	public void Prime( float delaySeconds = PrimingDuration )
	{
		if ( IsPrimed || IsDetonated ) return;

		IsPrimed = true;
		_detonationTimer = delaySeconds;

		// Diffuse l'effet d'amorçage sur tous les clients
		BroadcastPrimeFxRpc();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BroadcastPrimeFxRpc()
	{
		IsPrimed = true;

		if ( _kanjiRenderer.IsValid() )
			_kanjiRenderer.Tint = Color.Red;

		if ( _warningLight.IsValid() )
			_warningLight.Enabled = true;

		// Son de sifflement d'amorce / mèche
		var hissSound = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-generic.sound" )
			?? ResourceLibrary.Get<SoundEvent>( "sounds/box_shine.sound" );

		if ( hissSound is not null )
			Sound.Play( hissSound, WorldPosition );
	}

	/// <summary>
	/// Déclenche l'explosion : dégâts de zone, projection physique et effets Katon spectaculaires.
	/// Hôte seulement.
	/// </summary>
	public void ExecuteDetonation()
	{
		if ( !Networking.IsHost || IsDetonated ) return;
		IsDetonated = true;

		var center = WorldPosition;
		var normal = WorldRotation.Up;

		// 1. Dégâts aux joueurs shinobi
		foreach ( var player in Scene.GetAllComponents<NinjaPlayer>() )
		{
			if ( !player.IsValid() ) continue;

			var stats = player.GetComponent<NinjaStats>();
			if ( stats is null || stats.IsDowned ) continue;

			var dist = (player.WorldPosition - center).Length;
			if ( dist > ExplosionRadius ) continue;

			// Atténuation des dégâts selon la distance (de 100 % au centre à 30 % au bord)
			var falloff = Math.Clamp( 1f - (dist / ExplosionRadius) * 0.70f, 0.30f, 1f );
			var dmg = MathF.Round( MaxDamage * falloff );

			stats.TakeDamage( dmg, Owner );
		}

		// 2. Dégâts aux bandits
		foreach ( var bandit in Scene.GetAllComponents<AiBanditController>() )
		{
			if ( !bandit.IsValid() || bandit.IsDefeated ) continue;

			var dist = (bandit.WorldPosition - center).Length;
			if ( dist > ExplosionRadius ) continue;

			var falloff = Math.Clamp( 1f - (dist / ExplosionRadius) * 0.70f, 0.30f, 1f );
			var dmg = MathF.Round( MaxDamage * falloff );

			bandit.TakeDamage( dmg, Owner );
		}

		// 3. Souffle et projection physique sur les Rigidbodies
		foreach ( var rb in Scene.GetAllComponents<Rigidbody>() )
		{
			if ( !rb.IsValid() ) continue;

			var dist = (rb.WorldPosition - center).Length;
			if ( dist > ExplosionRadius ) continue;

			var dir = (rb.WorldPosition - center).Normal;
			if ( dir.LengthSquared < 0.01f ) dir = Vector3.Up;

			var force = (1f - dist / ExplosionRadius) * 450f;
			rb.Velocity += dir * force + Vector3.Up * 120f;
		}

		// 4. Notification au NinjaConsumables du poseur pour mettre à jour le compteur
		if ( Owner.IsValid() )
		{
			Owner.GetComponent<NinjaConsumables>()?.NotifyTagDetonated( this );
		}

		// 5. Diffusion des effets visuels et sonores d'explosion à tous les clients
		BroadcastExplosionFxRpc( center, normal );

		// 6. Destruction de l'objet parchemin
		GameObject.Destroy();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BroadcastExplosionFxRpc( Vector3 position, Vector3 normal )
	{
		// Palette Katon feu & braises
		var pal = FxPalette.FromNature( ChakraNature.Katon, new Color( 1f, 0.45f, 0.1f ) );

		var fxGo = JutsuFxKit.Root( "ExplosiveTagFx", position, 2.8f );

		// Boule de feu et langues d'explosion
		JutsuFxKit.Explosion( fxGo, pal, 130f );

		// Lumière dynamique d'éclairage de la scène
		JutsuFxKit.Light( fxGo, pal.Body * 3f, 320f, 1.2f, 0.2f );

		// Secousse caméra pour les joueurs proches
		JutsuFxKit.Shake( position, 7.5f, 650f, 0.45f, 25f );

		// Trace de brûlure au sol ou sur le mur
		JutsuFxKit.Scorch( position, normal, 70f );

		// Son d'explosion détonant
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/effects/explosion/explosion_small.sound" )
			?? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_world.sound" );

		if ( snd is not null )
			Sound.Play( snd, position );
	}

	/// <summary>
	/// Fait détoner tous les parchemins explosifs appartenant au propriétaire spécifié (« Katsu ! »).
	/// </summary>
	public static int DetonateAllBy( GameObject owner )
	{
		if ( !Networking.IsHost || !owner.IsValid() ) return 0;

		var count = 0;
		var tags = DeployedTags.Where( t => t.IsValid() && t.Owner == owner && !t.IsDetonated ).ToList();

		foreach ( var tag in tags )
		{
			// Amorçage rapide (0.12s) pour un Katsu quasi-instantané et réactif
			tag.Prime( 0.12f );
			count++;
		}

		return count;
	}

	/// <summary>
	/// Déploie un nouveau parchemin explosif dans le monde à l'endroit visé.
	/// </summary>
	public static ExplosiveTag Deploy( Scene scene, Vector3 position, Vector3 normal, GameObject owner, GameObject surfaceParent = null )
	{
		if ( scene is null ) return null;

		var tagGo = new GameObject( true, "ExplosiveTag" );
		tagGo.WorldPosition = position;

		// Orientation à plat contre la surface
		tagGo.WorldRotation = Rotation.LookAt( normal ) * Rotation.From( 90f, 0f, 0f );

		// Si posé sur un objet dynamique (porte, caisse), attachement au parent
		if ( surfaceParent.IsValid() && !surfaceParent.Components.GetInAncestorsOrSelf<NinjaPlayer>().IsValid() )
		{
			tagGo.SetParent( surfaceParent );
		}

		var tag = tagGo.Components.Create<ExplosiveTag>();
		tag.Owner = owner;

		// Objet réseau : sans ça les autres joueurs ne voient pas le parchemin.
		tagGo.NetworkSpawn( new NetworkSpawnOptions { OwnerTransfer = OwnerTransfer.Fixed } );

		return tag;
	}

	protected override void OnDestroy()
	{
		if ( Networking.IsHost )
		{
			DeployedTags.Remove( this );
		}
	}
}
