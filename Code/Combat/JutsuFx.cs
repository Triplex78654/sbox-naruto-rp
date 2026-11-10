using NarutoRp.Jutsus;

namespace NarutoRp.Combat;

/// <summary>
/// Effets visuels et sonores des jutsus, créés localement chez chaque client (objets non réseau).
/// Construits en code avec des assets du moteur. Les jutsus Katon utilisent le kit d'effets élémentaires
/// (JutsuFxKit : flammes en particules, traînée, braises, explosion, onde, brûlure, tremblement) ;
/// les autres gardent l'effet simple (sphère + lumière) en attendant leur variante.
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

	/// <summary>Petite lueur sur le lanceur pendant les signes.</summary>
	public static void Cast( JutsuDefinition jutsu, Vector3 position )
	{
		if ( Disabled || jutsu is null ) return;
		if ( jutsu.CastSound is not null ) Sound.Play( jutsu.CastSound, position );
		if ( IsElemental( jutsu ) ) { ElementalCast( jutsu, position ); return; }

		var go = Create( "Jutsu FX cast", position, jutsu.FxColor );
		var burst = go.Components.Create<JutsuFxBurst>();
		burst.MaxRadius = 14f;
		burst.Duration = MathF.Max( 0.2f, jutsu.CastSeconds );
	}

	/// <summary>Boule qui avance en ligne droite (le vrai projectile est simulé par l'hôte).</summary>
	public static void Projectile( JutsuDefinition jutsu, Vector3 start, Vector3 direction, GameObject caster )
	{
		if ( Disabled || jutsu is null ) return;

		GameObject go;
		if ( IsElemental( jutsu ) )
		{
			go = JutsuFxKit.Root( "Jutsu FX projectile", start, 0 );
			var pal = FxPalette.From( jutsu.FxColor );
			var r = MathF.Max( 8f, jutsu.HitRadius * jutsu.FxSize );
			JutsuFxKit.FlameBall( go, pal, r );
			JutsuFxKit.Trail( go, pal, r * 0.7f );
			JutsuFxKit.Embers( go, pal, r * 0.5f, 0, 30, 1, r * 2f, max: 30 ).Components.Get<ParticleSphereEmitter>().Loop = true;
			JutsuFxKit.Light( go, pal.Body * 2f, r * 9f, 0 );
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

	/// <summary>Jutsus qui ont leur effet élémentaire (pour l'instant : le feu).</summary>
	static bool IsElemental( JutsuDefinition jutsu ) => jutsu.Nature == ChakraNature.Katon;

	/// <summary>Signes : lueur, braises et flammes qui montent autour du lanceur, onde au sol, lumière qui s'éteint.</summary>
	static void ElementalCast( JutsuDefinition jutsu, Vector3 position )
	{
		var seconds = MathF.Max( 0.3f, jutsu.CastSeconds );
		var pal = FxPalette.From( jutsu.FxColor );
		var go = JutsuFxKit.Root( "Jutsu FX cast", position, seconds + 1.2f );

		var glow = JutsuFxKit.Layer( go, "textures/particles/particle_glow_07.vtex", true, 10, 2, 1, 12, seconds );
		glow.Lifetime = new ParticleFloat( 0.15f, 0.3f );
		glow.Scale = JutsuFxKit.OverLife( 14f, 26f );
		glow.Gradient = pal.Core;
		glow.Alpha = JutsuFxKit.OverLife( 1f, 0f );
		glow.Brightness = 3f;

		var embers = JutsuFxKit.Embers( go, pal, 26f, 4, 45, seconds, 40f, max: 40 );
		embers.WorldPosition = position + Vector3.Down * 40f; // au niveau des jambes, elles montent le long du corps
		embers.Components.Get<ParticleSphereEmitter>().DistanceBias = new Vector3( 1, 1, 0.2f );

		// Aura de flammes qui montent le long du corps + onde au sol à la fin des signes.
		var aura = JutsuFxKit.Layer( go, "textures/particles/particle_noisesphere_color.vtex", true, 30, 22, 0, 40, seconds );
		aura.WorldPosition = position + Vector3.Down * 30f;
		aura.Components.Get<ParticleSphereEmitter>().DistanceBias = new Vector3( 1, 1, 0.3f );
		aura.Lifetime = new ParticleFloat( 0.3f, 0.5f );
		aura.Scale = JutsuFxKit.OverLife( 16f, 4f );
		aura.Gradient = pal.Body;
		aura.Alpha = JutsuFxKit.OverLife( 0.8f, 0f );
		aura.Force = true;
		aura.ForceDirection = Vector3.Up * 300f;

		var ground = Game.ActiveScene.Trace.Ray( position, position + Vector3.Down * 120f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit ) JutsuFxKit.GroundRing( ground.HitPosition, ground.Normal, pal, 40f, seconds );

		JutsuFxKit.Light( go, pal.Body, 160f, seconds + 0.3f, 0.25f );
	}

	/// <summary>Impact : explosion, lumière forte, onde et brûlure au sol, tremblement pour les joueurs proches.</summary>
	static void ElementalBurst( JutsuDefinition jutsu, Vector3 position, float radius )
	{
		var pal = FxPalette.From( jutsu.FxColor );
		radius = MathF.Max( 16f, radius );

		// Surface touchée la plus proche (le point d'impact est collé au mur ou au sol) : sa normale sort l'explosion du mur.
		// Rayons tirés de l'extérieur vers le point (sol + 8 directions) : celui qui touche le plus près du point donne la face.
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
		JutsuFxKit.Explosion( go, pal, radius );
		JutsuFxKit.Light( go, pal.Body * 3f, radius * 10f, 1.4f, 0.2f );
		JutsuFxKit.Shake( center, MathF.Min( 8f, radius * 0.12f ), radius * 14f );

		// Onde de choc sur le sol sous l'explosion (les joueurs ne comptent pas).
		var ground = Game.ActiveScene.Trace.Ray( center, center + Vector3.Down * radius * 3f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit && !ground.StartedSolid ) JutsuFxKit.GroundRing( ground.HitPosition, ground.Normal, pal, radius * 1.6f );

		// Brûlure sur la surface touchée. Les décalques ne s'affichent pas sur le Terrain (vérifié) : pas de brûlure au sol nu.
		if ( surface.Hit && surface.GameObject?.Components.Get<Terrain>() is null )
			JutsuFxKit.Scorch( surface.HitPosition, surface.Normal, radius * 2.2f );
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
			// Effet en particules : on laisse finir les flammes déjà lancées au lieu de tout couper.
			if ( Components.Get<JutsuFxTimer>() is null && GetComponentInChildren<ParticleEffect>() is not null ) { JutsuFxKit.Release( GameObject ); Enabled = false; }
			else GameObject.Destroy();
			return;
		}
		WorldPosition = next;
	}
}
