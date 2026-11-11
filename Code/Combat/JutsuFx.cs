using NarutoRp.Jutsus;

namespace NarutoRp.Combat;

/// <summary>
/// Effets visuels et sonores des jutsus, créés localement chez chaque client (objets non réseau).
/// Construits en code avec des assets du moteur. Les 5 natures élémentaires (Katon, Suiton, Raiton,
/// Fûton, Doton) utilisent le kit d'effets élémentaires (JutsuFxKit) : particules stylisées, traînées,
/// explosions, gerbes d'eau, arcs électriques, tourbillons d'air, rochers et poussière, ondes au sol,
/// secousses de caméra et lumières dynamiques.
/// Purement décoratif : aucune logique de jeu ici.
/// </summary>
public static class JutsuFx
{
	const string SphereModel = "models/dev/sphere.vmdl";

	static bool Disabled => Application.IsDedicatedServer || Game.ActiveScene is null;

	/// <summary>Éclat qui grossit puis s'éteint (impact, zone, soin).</summary>
	public static void Burst( JutsuDefinition jutsu, Vector3 position, float radius )
	{
		if ( Disabled || jutsu is null ) return;
		if ( jutsu.ImpactSound is not null ) Sound.Play( jutsu.ImpactSound, position );
		if ( IsElemental( jutsu ) ) { ElementalBurst( jutsu, position, radius ); return; }

		var go = Create( "Jutsu FX", position, jutsu.FxColor );
		var burst = go.Components.Create<JutsuFxBurst>();
		burst.MaxRadius = MathF.Max( 16f, radius );
	}

	/// <summary>Lueur et particules d'incantation sur le lanceur pendant les signes.</summary>
	public static void Cast( JutsuDefinition jutsu, Vector3 position, GameObject caster = null )
	{
		if ( Disabled || jutsu is null ) return;
		if ( jutsu.CastSound is not null ) Sound.Play( jutsu.CastSound, position );
		if ( IsElemental( jutsu ) ) { ElementalCast( jutsu, position, caster ); return; }

		var go = Create( "Jutsu FX cast", position, jutsu.FxColor );
		if ( caster.IsValid() )
		{
			var follow = go.Components.Create<JutsuFxFollow>();
			follow.Target = caster;
			follow.Offset = position - caster.WorldPosition;
		}
		var burst = go.Components.Create<JutsuFxBurst>();
		burst.MaxRadius = 14f;
		burst.Duration = MathF.Max( 0.2f, jutsu.CastSeconds );
	}

	/// <summary>Boule ou projectile qui avance en ligne droite (le vrai projectile est simulé par l'hôte).</summary>
	public static void Projectile( JutsuDefinition jutsu, Vector3 start, Vector3 direction, GameObject caster )
	{
		if ( Disabled || jutsu is null ) return;

		GameObject go;
		if ( IsElemental( jutsu ) )
		{
			go = JutsuFxKit.Root( "Jutsu FX projectile", start, 0 );
			var pal = FxPalette.For( jutsu.Nature, jutsu.FxColor );
			var r = MathF.Max( 8f, jutsu.HitRadius * jutsu.FxSize );

			switch ( jutsu.Nature )
			{
				case ChakraNature.Katon:
					JutsuFxKit.FlameBall( go, pal, r );
					JutsuFxKit.Trail( go, pal, r * 0.7f );
					JutsuFxKit.Embers( go, pal, r * 0.5f, 0, 30, 1, r * 2f, max: 30 ).Components.Get<ParticleSphereEmitter>().Loop = true;
					JutsuFxKit.Light( go, pal.Body * 2f, r * 9f, 0 );
					break;

				case ChakraNature.Suiton:
					JutsuFxKit.WaterOrb( go, pal, r );
					JutsuFxKit.Trail( go, pal, r * 0.75f );
					JutsuFxKit.WaterDroplets( go, pal, r * 0.5f, 0, 25, 1, r * 1.5f, max: 25 ).Components.Get<ParticleSphereEmitter>().Loop = true;
					JutsuFxKit.Light( go, pal.Body * 1.8f, r * 8f, 0, flicker: 0.1f );
					break;

				case ChakraNature.Raiton:
					JutsuFxKit.LightningOrb( go, pal, r );
					JutsuFxKit.Trail( go, pal, r * 0.45f );
					JutsuFxKit.LightningSparks( go, pal, r * 0.4f, 0, 30, 1, r * 2.5f, max: 30 ).Components.Get<ParticleSphereEmitter>().Loop = true;
					JutsuFxKit.Light( go, pal.Body * 2.5f, r * 11f, 0, flicker: 0.45f );
					break;

				case ChakraNature.Futon:
					JutsuFxKit.WindBlade( go, pal, r );
					JutsuFxKit.Trail( go, pal, r * 0.6f );
					JutsuFxKit.Leaves( go, pal, r * 0.4f, 0, 15, 1, r * 1.2f, max: 20 ).Components.Get<ParticleSphereEmitter>().Loop = true;
					JutsuFxKit.Light( go, pal.Body * 1.4f, r * 7f, 0, flicker: 0.08f );
					break;

				case ChakraNature.Doton:
					JutsuFxKit.RockProjectile( go, pal, r );
					JutsuFxKit.Trail( go, pal, r * 0.8f );
					JutsuFxKit.RockDebris( go, pal, r * 0.5f, 0, 20, 1, r * 1.8f, max: 25 ).Components.Get<ParticleSphereEmitter>().Loop = true;
					JutsuFxKit.Light( go, pal.Body * 1.5f, r * 6f, 0, flicker: 0.12f );
					break;
			}
		}
		else
		{
			go = Create( "Jutsu FX projectile", start, jutsu.FxColor );
			go.WorldScale = MathF.Max( 0.1f, jutsu.HitRadius / 32f * jutsu.FxSize );
		}

		var p = go.Components.Create<JutsuFxProjectile>();
		p.Velocity = direction.Normal * MathF.Max( 1f, jutsu.ProjectileSpeed );
		p.Range = jutsu.Range;
		p.Ignore = caster;
		p.HitRadius = jutsu.HitRadius;
	}

	/// <summary>Jutsus qui ont leur effet élémentaire complet (les 5 natures).</summary>
	static bool IsElemental( JutsuDefinition jutsu ) =>
		jutsu.Nature is ChakraNature.Katon or ChakraNature.Suiton or ChakraNature.Raiton or ChakraNature.Futon or ChakraNature.Doton;

	/// <summary>Signes : particules élémentaires montant le long du corps, onde au sol, lueur et lumière.</summary>
	static void ElementalCast( JutsuDefinition jutsu, Vector3 position, GameObject caster )
	{
		var seconds = MathF.Max( 0.3f, jutsu.CastSeconds );
		var pal = FxPalette.For( jutsu.Nature, jutsu.FxColor );
		var go = JutsuFxKit.Root( "Jutsu FX cast", position, seconds + 1.2f );

		if ( caster.IsValid() )
		{
			var follow = go.Components.Create<JutsuFxFollow>();
			follow.Target = caster;
			follow.Offset = position - caster.WorldPosition;
		}

		switch ( jutsu.Nature )
		{
			case ChakraNature.Katon:
				JutsuFxKit.KatonCast( go, pal, seconds, position );
				break;

			case ChakraNature.Suiton:
				JutsuFxKit.SuitonCast( go, pal, seconds, position );
				break;

			case ChakraNature.Raiton:
				JutsuFxKit.RaitonCast( go, pal, seconds, position );
				break;

			case ChakraNature.Futon:
				JutsuFxKit.FutonCast( go, pal, seconds, position );
				break;

			case ChakraNature.Doton:
				JutsuFxKit.DotonCast( go, pal, seconds, position );
				break;
		}
	}

	/// <summary>Impact : explosion/gerbe/foudre/vent/roche, lumière, onde et décalque au sol, tremblement de caméra.</summary>
	static void ElementalBurst( JutsuDefinition jutsu, Vector3 position, float radius )
	{
		var pal = FxPalette.For( jutsu.Nature, jutsu.FxColor );
		radius = MathF.Max( 16f, radius );

		// Jutsu de zone (Area) : onde et effet circulaire étendu sur le terrain
		if ( jutsu.Delivery == JutsuDeliveryType.Area )
		{
			ElementalArea( jutsu, pal, position, radius );
			return;
		}

		// Surface touchée la plus proche (le point d'impact est collé au mur ou au sol) : sa normale sort l'explosion du mur.
		SceneTraceResult surface = default;
		var best = float.MaxValue;
		for ( int i = -1; i < 8; i++ )
		{
			var dir = i < 0 ? Vector3.Down : Rotation.FromYaw( i * 45f ).Forward;
			var t = Game.ActiveScene.Trace.Ray( position - dir * radius, position + dir * radius * 0.5f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
			if ( !t.Hit || t.StartedSolid ) continue;
			var d = t.HitPosition.Distance( position );
			if ( d < best ) { best = d; surface = t; }
		}
		var center = surface.Hit ? surface.HitPosition + surface.Normal * radius * 0.5f : position;

		var go = JutsuFxKit.Root( "Jutsu FX impact", center, 2.6f );

		switch ( jutsu.Nature )
		{
			case ChakraNature.Katon:
				JutsuFxKit.Explosion( go, pal, radius );
				JutsuFxKit.Light( go, pal.Body * 3f, radius * 10f, 1.4f, 0.2f );
				JutsuFxKit.Shake( center, MathF.Min( 8f, radius * 0.12f ), radius * 14f, 0.45f, 25f );
				break;

			case ChakraNature.Suiton:
				JutsuFxKit.WaterSplash( go, pal, radius );
				JutsuFxKit.Light( go, pal.Body * 2.2f, radius * 8f, 1.2f, 0.1f );
				JutsuFxKit.Shake( center, MathF.Min( 5f, radius * 0.08f ), radius * 10f, 0.4f, 18f );
				break;

			case ChakraNature.Raiton:
				JutsuFxKit.LightningBurst( go, pal, radius );
				JutsuFxKit.Light( go, pal.Core * 3.5f, radius * 12f, 0.9f, 0.45f );
				JutsuFxKit.Shake( center, MathF.Min( 9f, radius * 0.14f ), radius * 15f, 0.25f, 38f );
				break;

			case ChakraNature.Futon:
				JutsuFxKit.WindBurst( go, pal, radius );
				JutsuFxKit.Light( go, pal.Body * 1.5f, radius * 7f, 1.0f, 0.08f );
				JutsuFxKit.Shake( center, MathF.Min( 7f, radius * 0.10f ), radius * 12f, 0.35f, 22f );
				break;

			case ChakraNature.Doton:
				JutsuFxKit.EarthQuake( go, pal, radius );
				JutsuFxKit.Light( go, pal.Body * 1.8f, radius * 7f, 1.3f, 0.12f );
				JutsuFxKit.Shake( center, MathF.Min( 10f, radius * 0.16f ), radius * 18f, 0.65f, 15f );
				break;
		}

		// Onde de choc sur le sol sous l'explosion
		var ground = Game.ActiveScene.Trace.Ray( center, center + Vector3.Down * radius * 3f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit && !ground.StartedSolid )
			JutsuFxKit.GroundRing( ground.HitPosition, ground.Normal, pal, radius * 1.6f );

		// Décalque sur la surface touchée (ne s'affiche pas sur le Terrain, uniquement props/murs)
		if ( surface.Hit && surface.GameObject?.Components.Get<Terrain>() is null )
		{
			switch ( jutsu.Nature )
			{
				case ChakraNature.Katon:
				case ChakraNature.Raiton:
					JutsuFxKit.Scorch( surface.HitPosition, surface.Normal, radius * 2.2f );
					break;
				case ChakraNature.Doton:
					JutsuFxKit.ImpactDecal( surface.HitPosition, surface.Normal, radius * 2.0f );
					break;
			}
		}
	}

	/// <summary>Effet de zone au sol (Vague Suiton, Champ électrique Raiton, Rafale Fûton, Séisme Doton).</summary>
	static void ElementalArea( JutsuDefinition jutsu, FxPalette pal, Vector3 position, float radius )
	{
		var ground = Game.ActiveScene.Trace.Ray( position, position + Vector3.Down * 150f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		var center = ground.Hit ? ground.HitPosition + Vector3.Up * 5f : position;
		var normal = ground.Hit ? ground.Normal : Vector3.Up;

		var go = JutsuFxKit.Root( "Jutsu FX area", center, 3.2f );

		switch ( jutsu.Nature )
		{
			case ChakraNature.Suiton: // ex. Suiton : Vague déferlante
				JutsuFxKit.WaterGeyser( go, pal, radius, 1.8f );
				JutsuFxKit.GroundRing( center, normal, pal, radius * 1.2f, 1.2f );
				JutsuFxKit.Light( go, pal.Body * 2.5f, radius * 2f, 1.8f, 0.1f );
				JutsuFxKit.Shake( center, 6f, radius * 2.5f, 0.6f, 20f );
				break;

			case ChakraNature.Raiton: // ex. Raiton : Champ électrique
				JutsuFxKit.LightningField( go, pal, radius, 1.6f );
				JutsuFxKit.GroundRing( center, normal, pal, radius * 1.1f, 0.8f );
				JutsuFxKit.Light( go, pal.Core * 3.5f, radius * 2.2f, 1.5f, 0.45f );
				JutsuFxKit.Shake( center, 8f, radius * 2.5f, 0.5f, 35f );
				break;

			case ChakraNature.Futon: // ex. Fûton : Rafale
				JutsuFxKit.WindBurst( go, pal, radius * 0.7f );
				JutsuFxKit.GroundRing( center, normal, pal, radius * 1.3f, 1.0f );
				JutsuFxKit.Light( go, pal.Body * 1.8f, radius * 1.8f, 1.4f, 0.08f );
				JutsuFxKit.Shake( center, 7f, radius * 2.2f, 0.55f, 24f );
				break;

			case ChakraNature.Doton: // ex. Doton : Séisme
				JutsuFxKit.EarthRupture( go, pal, radius, 2.0f );
				JutsuFxKit.GroundRing( center, normal, pal, radius * 1.4f, 1.4f );
				JutsuFxKit.Light( go, pal.Body * 2.0f, radius * 2f, 1.8f, 0.12f );
				JutsuFxKit.Shake( center, 11f, radius * 3f, 0.85f, 14f );
				break;

			default:
				JutsuFxKit.Explosion( go, pal, radius * 0.6f );
				JutsuFxKit.GroundRing( center, normal, pal, radius * 1.2f, 1.2f );
				JutsuFxKit.Light( go, pal.Body * 3f, radius * 2f, 1.6f, 0.2f );
				JutsuFxKit.Shake( center, 8f, radius * 2.2f, 0.5f, 25f );
				break;
		}
	}

	static GameObject Create( string name, Vector3 position, Color color )
	{
		var go = new GameObject( true, name ) { WorldPosition = position, NetworkMode = NetworkMode.Never };

		var model = go.Components.Create<ModelRenderer>();
		model.Model = Model.Load( SphereModel );
		model.Tint = color;

		var light = go.Components.Create<PointLight>();
		light.LightColor = color;
		light.Radius = 200f;
		light.Shadows = false;
		return go;
	}
}

/// <summary>Anime un éclat : grossit jusqu'à MaxRadius puis disparaît.</summary>
public sealed class JutsuFxBurst : Component
{
	public float MaxRadius { get; set; } = 64f;
	public float Duration { get; set; } = 0.45f;

	TimeSince _age;

	protected override void OnStart() => _age = 0;

	protected override void OnUpdate()
	{
		var t = _age / Duration;
		if ( t >= 1f ) { GameObject.Destroy(); return; }

		// Le modèle de sphère fait environ 32 unités de rayon à l'échelle 1.
		WorldScale = MaxRadius / 32f * (1f - (1f - t) * (1f - t)); // départ rapide, fin douce
		var light = GetComponent<PointLight>();
		if ( light is not null ) light.Radius = MaxRadius * 3f * (1f - t);
	}
}

/// <summary>Déplace une boule de jutsu en ligne droite jusqu'à sa portée ou un obstacle.</summary>
public sealed class JutsuFxProjectile : Component
{
	public Vector3 Velocity { get; set; }
	public float Range { get; set; } = 1000f;

	/// <summary>Le lanceur : la boule part de l'intérieur de sa capsule, il ne doit pas l'arrêter.</summary>
	public GameObject Ignore { get; set; }

	/// <summary>Même rayon de touche que le projectile de l'hôte.</summary>
	public float HitRadius { get; set; } = 16f;

	float _travelled;

	protected override void OnUpdate()
	{
		var step = Velocity * Time.Delta;
		var next = WorldPosition + step;
		var trace = Scene.Trace.Ray( WorldPosition, next ).Radius( HitRadius ).WithoutTags( Core.NetConstants.TagInvisible );
		if ( Ignore.IsValid() ) trace = trace.IgnoreGameObjectHierarchy( Ignore );
		var tr = trace.Run();
		_travelled += step.Length;

		if ( tr.Hit || _travelled >= Range )
		{
			// Effet en particules : on laisse finir les particules déjà lancées au lieu de tout couper brutalement.
			if ( Components.Get<JutsuFxTimer>() is null && GetComponentInChildren<ParticleEffect>() is not null )
			{
				JutsuFxKit.Release( GameObject );
				Enabled = false;
			}
			else
			{
				GameObject.Destroy();
			}
			return;
		}
		WorldPosition = next;
	}
}
