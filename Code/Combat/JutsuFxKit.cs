using NarutoRp.Jutsus;

namespace NarutoRp.Combat;

/// <summary>
/// Couleurs d'un effet élémentaire : coeur brillant, corps, bords sombres, fumée/brume et accent/étincelle.
/// Dérivée de la nature du chakra et de la couleur de la fiche (FxColor).
/// </summary>
public readonly record struct FxPalette( Color Core, Color Body, Color Dark, Color Smoke, Color Accent )
{
	public FxPalette( Color Core, Color Body, Color Dark, Color Smoke ) : this( Core, Body, Dark, Smoke, Body ) { }

	public static FxPalette From( Color c ) => FromNature( ChakraNature.None, c );

	public static FxPalette For( ChakraNature nature, Color c ) => FromNature( nature, c );

	public static FxPalette FromNature( ChakraNature nature, Color c )
	{
		return nature switch
		{
			ChakraNature.Katon => new(
				new Color( 1f, 0.95f, 0.72f ),                      // coeur blanc-jaune incandescent
				c.a > 0.01f ? c : new Color( 1f, 0.45f, 0.1f ),      // corps orange feu vif
				new Color( 0.52f, 0.09f, 0.03f ),                   // rouge sombre braise
				new Color( 0.12f, 0.1f, 0.09f ),                    // fumée grise chaude
				new Color( 1f, 0.82f, 0.22f ) ),                    // étincelle jaune

			ChakraNature.Suiton => new(
				new Color( 0.88f, 0.96f, 1f ),                      // écume blanche bleutée
				c.a > 0.01f ? c : new Color( 0.2f, 0.55f, 0.98f ),  // bleu azur eau limpide
				new Color( 0.04f, 0.2f, 0.52f ),                    // bleu profond des abysses
				new Color( 0.72f, 0.86f, 0.96f ),                   // brume d'eau vaporeuse
				new Color( 0.35f, 0.85f, 1f ) ),                    // turquoise écume

			ChakraNature.Raiton => new(
				new Color( 1f, 1f, 1f ),                            // blanc pur éclatant
				c.a > 0.01f ? c : new Color( 0.45f, 0.75f, 1f ),    // bleu électrique cyan
				new Color( 0.22f, 0.12f, 0.62f ),                   // indigo / violet foudre
				new Color( 0.25f, 0.4f, 0.75f ),                    // rémanence électrique
				new Color( 0.78f, 0.92f, 1f ) ),                    // flash bleu-blanc

			ChakraNature.Futon => new(
				new Color( 0.96f, 1f, 0.98f ),                      // blanc air comprimé
				c.a > 0.01f ? c : new Color( 0.68f, 0.96f, 0.82f ), // jade / brise aérienne translucide
				new Color( 0.32f, 0.58f, 0.42f ),                   // vert feuille grisé
				new Color( 0.86f, 0.9f, 0.85f ),                    // poussière fine de vent
				new Color( 0.48f, 0.82f, 0.56f ) ),                 // vert menthe vif

			ChakraNature.Doton => new(
				new Color( 0.86f, 0.74f, 0.52f ),                   // sable ocre clair / calcaire
				c.a > 0.01f ? c : new Color( 0.58f, 0.4f, 0.24f ),  // terre battue brune
				new Color( 0.26f, 0.18f, 0.12f ),                   // roche granit sombre
				new Color( 0.5f, 0.42f, 0.32f ),                    // poussière ocre terreuse
				new Color( 0.42f, 0.32f, 0.22f ) ),                 // éclat de roche ardoise

			_ => new(
				new Color( Mix( c.r, 1f, 0.75f ), Mix( c.g, 1f, 0.6f ), Mix( c.b, 1f, 0.35f ) ),
				c,
				new Color( c.r * 0.7f, c.g * 0.25f, c.b * 0.2f ),
				new Color( 0.12f, 0.1f, 0.09f ),
				c )
		};
	}

	static float Mix( float a, float b, float t ) => a + (b - a) * t;
}

/// <summary>
/// Briques d'effets visuels élémentaires pour Katon, Suiton, Raiton, Fûton, Doton.
/// Client seulement, objets non réseau, aucune logique de jeu.
/// Budget strict par effet pour préserver le framerate (> 160 FPS).
/// </summary>
public static class JutsuFxKit
{
	// Textures de base et halos (core/textures/particles/)
	const string TexGlow = "textures/particles/particle_glow_01.vtex";
	const string TexCore = "textures/particles/particle_glow_07.vtex";
	const string TexNoise = "textures/particles/particle_noisesphere_color.vtex";
	const string TexRing = "textures/particles/particle_ring_wave.vtex";

	// Katon
	const string TexFlame = "textures/particles/particle_noisesphere_color.vtex";
	const string TexLick = "textures/particles/muzzleflash/muzzleflash.vtex";
	const string TexSmoke = "textures/particles/particle_noisesphere_color.vtex";
	const string TexEmber = "textures/particles/flame/ember.vtex";
	const string ScorchDecal = "decals/scorchmarks/decal_scorchmark_01.decal";

	// Suiton
	const string TexWaterSplash = "textures/particles/fluid/watersplash-1.vtex";
	const string TexWaterSplashSide = "textures/particles/fluid/watersplash_side.vtex";
	const string TexDroplet = "textures/particles/fluid/droplet_a.vtex";
	const string TexWaterDrop = "textures/particles/fluid/water_drop.vtex";

	// Raiton
	const string TexSparks = "textures/particles/sparks.vtex";
	const string TexSparkWhite = "textures/particles/spark_white.vtex";
	const string TexFlare4 = "textures/particles/flares/particle_flare_4.vtex";
	const string TexFlare7 = "textures/particles/flares/particle_flare_7b.vtex";
	const string TexWarpBurst = "textures/particles/warp5_explosion.vtex";

	// Fûton
	const string TexSwirl = "textures/particles/particle_swirl_03.vtex";
	const string TexVortex = "textures/particles/ring_wave_vortex.vtex";
	const string TexLeafGreen = "textures/particles/leaves/leaf.vtex";
	const string TexLeafDead = "textures/particles/leaves/leaf_dead_01.vtex";
	const string TexSmokeWispy = "textures/particles/smoke/smoke_wispy_a.vtex";
	const string TexSmokeWispyBurst = "textures/particles/smoke/smoke_wispy_burst.vtex";

	// Doton
	const string TexRockChunks = "textures/particles/impact/rock_chunks_01.vtex";
	const string TexConcreteChunks = "textures/particles/impact/concrete_chunks.vtex";
	const string TexDebris = "textures/particles/particle_debris_02.vtex";
	const string TexDustBurst = "textures/particles/smoke/smoke_burst_a.vtex";
	const string DirtDecal = "decals/impact/dirt/decal_impact_dirt_01.decal";

	static readonly Dictionary<string, Sprite> _sprites = new();

	static Sprite SpriteOf( string texture )
	{
		if ( !_sprites.TryGetValue( texture, out var s ) )
			_sprites[texture] = s = Sprite.FromTexture( Texture.Load( texture ) );
		return s;
	}

	/// <summary>Objet racine d'un effet, détruit au bout de <paramref name="seconds"/> (0 = jamais, le parent s'en charge).</summary>
	public static GameObject Root( string name, Vector3 position, float seconds )
	{
		var go = new GameObject( true, name ) { WorldPosition = position, NetworkMode = NetworkMode.Never };
		if ( seconds > 0f ) go.Components.Create<JutsuFxTimer>().Seconds = seconds;
		return go;
	}

	/// <summary>
	/// Une couche de sprites : un ParticleEffect + son rendu + un émetteur sphérique, sur un enfant de <paramref name="parent"/>.
	/// </summary>
	public static ParticleEffect Layer( GameObject parent, string texture, bool additive, int max, float radius, float burst, float rate, float duration, bool loop = false )
	{
		var go = new GameObject( parent, true, "fx layer" ) { NetworkMode = NetworkMode.Never };

		var fx = go.Components.Create<ParticleEffect>();
		fx.MaxParticles = max;
		fx.ApplyColor = true;
		fx.ApplyAlpha = true;
		fx.ApplyShape = true;
		fx.ApplyRotation = true;
		fx.Roll = new ParticleFloat( 0, 360 );

		var r = go.Components.Create<ParticleSpriteRenderer>();
		r.Sprite = SpriteOf( texture );
		r.Additive = additive;
		r.Lighting = false;
		r.Shadows = false;
		r.DepthFeather = 8f;

		var e = go.Components.Create<ParticleSphereEmitter>();
		e.Radius = radius;
		e.Velocity = 0;
		e.Burst = burst;
		e.Rate = rate;
		e.Duration = duration;
		e.Loop = loop;
		return fx;
	}

	/// <summary>Valeur qui va de a à b sur la vie de la particule.</summary>
	public static ParticleFloat OverLife( float a, float b ) => new( a, b ) { Evaluation = ParticleFloat.EvaluationType.Life };

	public static ParticleGradient ColorOverLife( Color a, Color b ) => new()
	{
		Type = ParticleGradient.ValueType.Range,
		Evaluation = ParticleGradient.EvaluationType.Life,
		ConstantA = a,
		ConstantB = b,
	};

	// ─── Briques communes ───────────────────────────────────────────────────

	/// <summary>Traînée lumineuse qui s'amincit derrière l'objet qui bouge.</summary>
	public static void Trail( GameObject parent, FxPalette pal, float width, float lifeTime = 0.3f, BlendMode blend = BlendMode.Lighten )
	{
		var go = new GameObject( parent, true, "fx trail" ) { NetworkMode = NetworkMode.Never };
		var t = go.Components.Create<TrailRenderer>();
		t.LifeTime = lifeTime;
		t.PointDistance = 6f;
		t.MaxPoints = 32;
		t.Opaque = false;
		t.BlendMode = blend;

		Curve w = width;
		w.AddPoint( 0f, width );
		w.AddPoint( 1f, 0f );
		t.Width = w;

		Gradient g = pal.Core;
		g.AddColor( 0.3f, pal.Body );
		g.AddColor( 1f, pal.Dark );
		g.AddAlpha( 0f, 0.65f );
		g.AddAlpha( 1f, 0f );
		t.Color = g;
	}

	/// <summary>Lumière dynamique qui éclaire la scène ; scintille et s'éteint sur <paramref name="seconds"/> (0 = reste allumée).</summary>
	public static PointLight Light( GameObject parent, Color color, float radius, float seconds, float flicker = 0.15f )
	{
		var go = new GameObject( parent, true, "fx light" ) { NetworkMode = NetworkMode.Never };
		var light = go.Components.Create<PointLight>();
		light.LightColor = color;
		light.Radius = radius;
		light.Shadows = false;
		var anim = go.Components.Create<JutsuFxLight>();
		anim.BaseRadius = radius;
		anim.BaseColor = color;
		anim.Seconds = seconds;
		anim.Flicker = flicker;
		return light;
	}

	/// <summary>Onde de choc à plat sur le sol (anneau qui s'élargit et s'efface).</summary>
	public static void GroundRing( Vector3 ground, Vector3 normal, FxPalette pal, float radius, float seconds = 0.5f )
	{
		var root = Root( "fx ring", ground + normal * 3f, seconds + 0.2f );
		var ring = Layer( root, TexRing, true, 2, 0, 1, 0, 0.1f );
		ring.Components.Get<ParticleSpriteRenderer>().Alignment = ParticleSpriteRenderer.BillboardAlignment.Particle;
		ring.Pitch = 90f;
		ring.Roll = 0f;
		ring.Lifetime = seconds;
		ring.Scale = OverLife( radius * 0.3f, radius * 2.2f );
		ring.Gradient = ColorOverLife( pal.Core, pal.Body );
		ring.Alpha = OverLife( 1f, 0f );
		ring.Brightness = 1.2f;
	}

	/// <summary>Trace de brûlure sur une surface solide, qui s'efface au bout de <paramref name="seconds"/>.</summary>
	public static void Scorch( Vector3 ground, Vector3 normal, float size, float seconds = 8f )
	{
		var def = ResourceLibrary.Get<DecalDefinition>( ScorchDecal );
		if ( def is null ) return;

		var root = Root( "fx scorch", ground + normal, seconds );
		root.WorldRotation = Rotation.LookAt( -normal );
		var d = root.Components.Create<Decal>();
		d.Decals = [def];
		d.Size = new Vector2( size, size ) / MathF.Max( 1f, def.Width );
		d.LifeTime = seconds;
		d.ColorTint = ColorOverLife( Color.White, Color.White.WithAlpha( 0f ) );
	}

	/// <summary>Trace d'impact de terre / fissure sur une surface solide.</summary>
	public static void ImpactDecal( Vector3 ground, Vector3 normal, float size, float seconds = 8f )
	{
		var def = ResourceLibrary.Get<DecalDefinition>( DirtDecal );
		if ( def is null ) return;

		var root = Root( "fx dirt decal", ground + normal, seconds );
		root.WorldRotation = Rotation.LookAt( -normal );
		var d = root.Components.Create<Decal>();
		d.Decals = [def];
		d.Size = new Vector2( size, size ) / MathF.Max( 1f, def.Width );
		d.LifeTime = seconds;
		d.ColorTint = ColorOverLife( Color.White, Color.White.WithAlpha( 0f ) );
	}

	/// <summary>Tremblement de caméra pour les joueurs proches : plein à <paramref name="position"/>, nul à <paramref name="radius"/>.</summary>
	public static void Shake( Vector3 position, float amplitude, float radius, float seconds = 0.45f, float frequency = 25f )
	{
		Game.ActiveScene.GetSystem<CameraEffectSystem>()?.AddShake( position, radius, amplitude, frequency, seconds );
	}

	/// <summary>Coupe l'émission (boule qui touche) et laisse les particules déjà lancées finir leur vie.</summary>
	public static void Release( GameObject root, float seconds = 1f )
	{
		foreach ( var e in root.Components.GetAll<ParticleEmitter>( FindMode.EverythingInSelfAndDescendants ) ) e.Enabled = false;
		foreach ( var t in root.Components.GetAll<TrailRenderer>( FindMode.EverythingInSelfAndDescendants ) ) t.Emitting = false;
		foreach ( var l in root.Components.GetAll<JutsuFxLight>( FindMode.EverythingInSelfAndDescendants ) ) l.FadeOut( 0.15f );
		root.Components.GetOrCreate<JutsuFxTimer>().Seconds = seconds;
	}

	// ─── Briques Katon (Feu) ────────────────────────────────────────────────

	/// <summary>Boule de flammes en couches : coeur blanc-jaune, corps orange, langues rouge sombre, fumée.</summary>
	public static void FlameBall( GameObject parent, FxPalette pal, float r )
	{
		var core = Layer( parent, TexCore, true, 12, r * 0.15f, 3, 40, 1, loop: true );
		core.LocalSpace = 1f;
		core.Lifetime = new ParticleFloat( 0.12f, 0.2f );
		core.Scale = new ParticleFloat( r * 1.3f, r * 1.6f );
		core.Gradient = pal.Core;
		core.Brightness = 1.2f;

		var body = Layer( parent, TexFlame, true, 45, r * 0.55f, 10, 90, 1, loop: true );
		body.Lifetime = new ParticleFloat( 0.22f, 0.4f );
		body.Scale = OverLife( r * 1.3f, r * 0.4f );
		body.Gradient = ColorOverLife( pal.Body, pal.Dark );
		body.Alpha = OverLife( 1f, 0f );
		body.Brightness = 0.9f;

		var licks = Layer( parent, TexLick, false, 24, r * 0.6f, 4, 35, 1, loop: true );
		licks.Lifetime = new ParticleFloat( 0.25f, 0.45f );
		licks.Scale = OverLife( r * 1.1f, r * 1.7f );
		licks.Gradient = ColorOverLife( pal.Body, pal.Dark );
		licks.Alpha = OverLife( 0.85f, 0f );
		licks.Brightness = 1.6f;

		var smoke = Layer( parent, TexSmoke, false, 14, r * 0.4f, 0, 12, 1, loop: true );
		smoke.Lifetime = new ParticleFloat( 0.6f, 0.9f );
		smoke.Scale = OverLife( r * 0.8f, r * 2f );
		smoke.Gradient = pal.Smoke;
		smoke.Alpha = OverLife( 0.45f, 0f );
		smoke.Force = true;
		smoke.ForceDirection = Vector3.Up * 60f;
	}

	/// <summary>Braises qui montent (étincelles) : en jet continu (rate) ou en gerbe (burst).</summary>
	public static ParticleEffect Embers( GameObject parent, FxPalette pal, float radius, float burst, float rate, float duration, float speed, int max = 40 )
	{
		var fx = Layer( parent, TexEmber, true, max, radius, burst, rate, duration );
		fx.Lifetime = new ParticleFloat( 0.5f, 1.1f );
		fx.Scale = OverLife( 7f, 1.5f );
		fx.StartVelocity = new ParticleFloat( speed * 0.3f, speed );
		fx.Damping = 1.5f;
		fx.Force = true;
		fx.ForceDirection = Vector3.Up * 120f;
		fx.Gradient = ColorOverLife( pal.Core, pal.Dark );
		fx.Alpha = OverLife( 1f, 0f );
		fx.Brightness = 2f;
		fx.Components.Get<ParticleSphereEmitter>().Velocity = speed;
		return fx;
	}

	/// <summary>Explosion Katon : flash, sphère de feu qui grossit, langues, braises, fumée et flammes qui traînent ~1,5 s.</summary>
	public static void Explosion( GameObject parent, FxPalette pal, float radius )
	{
		var flash = Layer( parent, TexGlow, true, 2, 0, 2, 0, 0.1f );
		flash.Lifetime = 0.18f;
		flash.Scale = OverLife( radius * 4f, radius * 6f );
		flash.Gradient = pal.Core;
		flash.Alpha = OverLife( 1f, 0f );
		flash.Brightness = 1.5f;

		var ball = Layer( parent, TexFlame, true, 30, radius * 0.3f, 26, 0, 0.1f );
		ball.Components.Get<ParticleSphereEmitter>().Velocity = radius * 5f;
		ball.Damping = 5f;
		ball.Lifetime = new ParticleFloat( 0.45f, 0.8f );
		ball.Scale = OverLife( radius * 1.2f, radius * 2.2f );
		ball.Gradient = ColorOverLife( pal.Body, pal.Dark );
		ball.Alpha = OverLife( 1f, 0f );
		ball.Brightness = 1f;

		var licks = Layer( parent, TexLick, true, 12, radius * 0.4f, 12, 0, 0.1f );
		licks.Components.Get<ParticleSphereEmitter>().Velocity = radius * 3f;
		licks.Damping = 4f;
		licks.Lifetime = new ParticleFloat( 0.3f, 0.6f );
		licks.Scale = OverLife( radius * 1.5f, radius * 2.5f );
		licks.Gradient = ColorOverLife( pal.Body, pal.Dark );
		licks.Alpha = OverLife( 0.9f, 0f );

		var linger = Layer( parent, TexFlame, true, 24, radius * 0.9f, 0, 14, 1.5f );
		linger.Lifetime = new ParticleFloat( 0.35f, 0.6f );
		linger.Scale = OverLife( radius * 0.8f, radius * 0.2f );
		linger.Gradient = ColorOverLife( pal.Body, pal.Dark );
		linger.Alpha = OverLife( 0.9f, 0f );
		linger.Force = true;
		linger.ForceDirection = Vector3.Up * 150f;
		linger.Brightness = 0.9f;

		var smoke = Layer( parent, TexSmoke, false, 16, radius * 0.6f, 6, 5, 1.6f );
		smoke.Lifetime = new ParticleFloat( 1.4f, 2.2f );
		smoke.Scale = OverLife( radius * 1.2f, radius * 3f );
		smoke.Gradient = pal.Smoke;
		smoke.Alpha = OverLife( 0.55f, 0f );
		smoke.Force = true;
		smoke.ForceDirection = Vector3.Up * 40f;
		smoke.Components.Get<ParticleSphereEmitter>().Velocity = radius;
		smoke.Damping = 2f;

		Embers( parent, pal, radius * 0.3f, 30, 10, 1.2f, radius * 8f );
	}

	/// <summary>Incantation Katon : lueur, braises et flammes montant le long du corps, lumière.</summary>
	public static void KatonCast( GameObject root, FxPalette pal, float seconds, Vector3 position )
	{
		var glow = Layer( root, TexCore, true, 10, 2, 1, 12, seconds );
		glow.Lifetime = new ParticleFloat( 0.15f, 0.3f );
		glow.Scale = OverLife( 14f, 26f );
		glow.Gradient = pal.Core;
		glow.Alpha = OverLife( 1f, 0f );
		glow.Brightness = 3f;

		var embers = Embers( root, pal, 26f, 4, 45, seconds, 40f, max: 40 );
		embers.WorldPosition = position + Vector3.Down * 40f;
		embers.Components.Get<ParticleSphereEmitter>().DistanceBias = new Vector3( 1, 1, 0.2f );

		var aura = Layer( root, TexFlame, true, 30, 22, 0, 40, seconds );
		aura.WorldPosition = position + Vector3.Down * 30f;
		aura.Components.Get<ParticleSphereEmitter>().DistanceBias = new Vector3( 1, 1, 0.3f );
		aura.Lifetime = new ParticleFloat( 0.3f, 0.5f );
		aura.Scale = OverLife( 16f, 4f );
		aura.Gradient = pal.Body;
		aura.Alpha = OverLife( 0.8f, 0f );
		aura.Force = true;
		aura.ForceDirection = Vector3.Up * 300f;

		var ground = Game.ActiveScene.Trace.Ray( position, position + Vector3.Down * 120f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit ) GroundRing( ground.HitPosition, ground.Normal, pal, 40f, seconds );

		Light( root, pal.Body, 160f, seconds + 0.3f, 0.25f );
	}

	// ─── Briques Suiton (Eau) ───────────────────────────────────────────────

	/// <summary>Orbe d'eau : masse liquide fluide, écume centrale, gouttelettes et brume d'eau en traînée.</summary>
	public static void WaterOrb( GameObject parent, FxPalette pal, float r )
	{
		// Coeur d'écume blanche
		var core = Layer( parent, TexCore, true, 10, r * 0.15f, 2, 35, 1, loop: true );
		core.LocalSpace = 1f;
		core.Lifetime = new ParticleFloat( 0.12f, 0.22f );
		core.Scale = new ParticleFloat( r * 1.0f, r * 1.3f );
		core.Gradient = pal.Core;
		core.Brightness = 1.4f;

		// Corps d'eau ondulant turquoise/azur
		var body = Layer( parent, TexNoise, true, 35, r * 0.5f, 8, 70, 1, loop: true );
		body.Lifetime = new ParticleFloat( 0.25f, 0.45f );
		body.Scale = OverLife( r * 1.3f, r * 0.5f );
		body.Gradient = ColorOverLife( pal.Body, pal.Dark );
		body.Alpha = OverLife( 0.85f, 0.1f );
		body.Brightness = 1.1f;

		// Brume vaporeuse s'échappant
		var mist = Layer( parent, TexWaterDrop, false, 12, r * 0.4f, 0, 15, 1, loop: true );
		mist.Lifetime = new ParticleFloat( 0.4f, 0.7f );
		mist.Scale = OverLife( r * 0.8f, r * 1.6f );
		mist.Gradient = pal.Smoke;
		mist.Alpha = OverLife( 0.4f, 0f );
	}

	/// <summary>Gouttelettes d'eau projetées ou s'élevant.</summary>
	public static ParticleEffect WaterDroplets( GameObject parent, FxPalette pal, float radius, float burst, float rate, float duration, float speed, int max = 30 )
	{
		var fx = Layer( parent, TexDroplet, true, max, radius, burst, rate, duration );
		fx.Lifetime = new ParticleFloat( 0.4f, 0.85f );
		fx.Scale = OverLife( 6f, 1.5f );
		fx.StartVelocity = new ParticleFloat( speed * 0.4f, speed );
		fx.Damping = 1.8f;
		fx.Force = true;
		fx.ForceDirection = Vector3.Down * 60f; // pesanteur douce
		fx.Gradient = ColorOverLife( pal.Core, pal.Accent );
		fx.Alpha = OverLife( 1f, 0f );
		fx.Brightness = 1.8f;
		fx.Components.Get<ParticleSphereEmitter>().Velocity = speed;
		return fx;
	}

	/// <summary>Gerbe d'eau et éclaboussures à l'impact : geyser d'eau, gouttelettes radiales, brume.</summary>
	public static void WaterSplash( GameObject parent, FxPalette pal, float radius )
	{
		// Flash d'impact écume
		var flash = Layer( parent, TexGlow, true, 2, 0, 2, 0, 0.1f );
		flash.Lifetime = 0.16f;
		flash.Scale = OverLife( radius * 3f, radius * 5f );
		flash.Gradient = pal.Core;
		flash.Alpha = OverLife( 1f, 0f );

		// Gerbe d'eau centrale montante
		var splash = Layer( parent, TexWaterSplash, true, 4, radius * 0.2f, 3, 0, 0.1f );
		splash.Lifetime = new ParticleFloat( 0.35f, 0.55f );
		splash.Scale = OverLife( radius * 1.4f, radius * 2.8f );
		splash.Gradient = ColorOverLife( pal.Core, pal.Body );
		splash.Alpha = OverLife( 1f, 0f );
		splash.Force = true;
		splash.ForceDirection = Vector3.Up * 180f;

		// Éclaboussures latérales
		var side = Layer( parent, TexWaterSplashSide, false, 8, radius * 0.4f, 8, 0, 0.1f );
		side.Components.Get<ParticleSphereEmitter>().Velocity = radius * 4f;
		side.Damping = 3f;
		side.Lifetime = new ParticleFloat( 0.4f, 0.65f );
		side.Scale = OverLife( radius * 1.1f, radius * 2.2f );
		side.Gradient = ColorOverLife( pal.Body, pal.Dark );
		side.Alpha = OverLife( 0.85f, 0f );

		// Pluie de gouttelettes d'eau retombant
		var drops = Layer( parent, TexDroplet, true, 28, radius * 0.3f, 26, 0, 0.1f );
		drops.Components.Get<ParticleSphereEmitter>().Velocity = radius * 6f;
		drops.Damping = 2.5f;
		drops.Force = true;
		drops.ForceDirection = Vector3.Down * 220f;
		drops.Lifetime = new ParticleFloat( 0.5f, 0.9f );
		drops.Scale = OverLife( 8f, 2f );
		drops.Gradient = ColorOverLife( pal.Core, pal.Accent );
		drops.Alpha = OverLife( 1f, 0f );
		drops.Brightness = 2f;

		// Brume d'eau qui s'évapore
		var mist = Layer( parent, TexNoise, false, 12, radius * 0.5f, 6, 4, 1.2f );
		mist.Lifetime = new ParticleFloat( 1.1f, 1.8f );
		mist.Scale = OverLife( radius * 1f, radius * 2.6f );
		mist.Gradient = pal.Smoke;
		mist.Alpha = OverLife( 0.4f, 0f );
		mist.Force = true;
		mist.ForceDirection = Vector3.Up * 30f;
		mist.Damping = 2f;
	}

	/// <summary>Geyser d'eau étendu pour la vague / zone Suiton.</summary>
	public static void WaterGeyser( GameObject parent, FxPalette pal, float radius, float seconds )
	{
		var geyser = Layer( parent, TexWaterSplash, true, 16, radius * 0.6f, 6, 12, seconds );
		geyser.Lifetime = new ParticleFloat( 0.5f, 0.85f );
		geyser.Scale = OverLife( radius * 0.6f, radius * 1.4f );
		geyser.Gradient = ColorOverLife( pal.Core, pal.Body );
		geyser.Alpha = OverLife( 0.95f, 0f );
		geyser.Force = true;
		geyser.ForceDirection = Vector3.Up * 280f;

		var side = Layer( parent, TexWaterSplashSide, false, 18, radius * 0.8f, 8, 14, seconds );
		side.Lifetime = new ParticleFloat( 0.45f, 0.75f );
		side.Scale = OverLife( radius * 0.5f, radius * 1.2f );
		side.Gradient = ColorOverLife( pal.Body, pal.Dark );
		side.Alpha = OverLife( 0.8f, 0f );

		var mist = Layer( parent, TexNoise, false, 20, radius * 0.8f, 10, 8, seconds );
		mist.Lifetime = new ParticleFloat( 1.2f, 2.0f );
		mist.Scale = OverLife( radius * 0.8f, radius * 2.2f );
		mist.Gradient = pal.Smoke;
		mist.Alpha = OverLife( 0.45f, 0f );
		mist.Force = true;
		mist.ForceDirection = Vector3.Up * 25f;

		WaterDroplets( parent, pal, radius * 0.7f, 20, 25, seconds, radius * 3f, max: 35 );
	}

	/// <summary>Incantation Suiton : tourbillon d'eau s'élevant aux pieds, gouttelettes, brume, lueur cyan.</summary>
	public static void SuitonCast( GameObject root, FxPalette pal, float seconds, Vector3 position )
	{
		var glow = Layer( root, TexCore, true, 8, 2, 1, 10, seconds );
		glow.Lifetime = new ParticleFloat( 0.15f, 0.3f );
		glow.Scale = OverLife( 14f, 24f );
		glow.Gradient = pal.Core;
		glow.Alpha = OverLife( 1f, 0f );
		glow.Brightness = 2.5f;

		var swirl = Layer( root, TexNoise, true, 25, 20f, 0, 35, seconds );
		swirl.WorldPosition = position + Vector3.Down * 30f;
		swirl.Components.Get<ParticleSphereEmitter>().DistanceBias = new Vector3( 1, 1, 0.3f );
		swirl.Lifetime = new ParticleFloat( 0.35f, 0.6f );
		swirl.Scale = OverLife( 14f, 3f );
		swirl.Gradient = ColorOverLife( pal.Body, pal.Accent );
		swirl.Alpha = OverLife( 0.85f, 0f );
		swirl.Force = true;
		swirl.ForceDirection = Vector3.Up * 240f;

		var drops = WaterDroplets( root, pal, 22f, 4, 30, seconds, 35f, max: 30 );
		drops.WorldPosition = position + Vector3.Down * 35f;

		var mist = Layer( root, TexNoise, false, 10, 18f, 0, 8, seconds );
		mist.WorldPosition = position + Vector3.Down * 25f;
		mist.Lifetime = new ParticleFloat( 0.6f, 1.1f );
		mist.Scale = OverLife( 12f, 28f );
		mist.Gradient = pal.Smoke;
		mist.Alpha = OverLife( 0.35f, 0f );
		mist.Force = true;
		mist.ForceDirection = Vector3.Up * 40f;

		var ground = Game.ActiveScene.Trace.Ray( position, position + Vector3.Down * 120f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit ) GroundRing( ground.HitPosition, ground.Normal, pal, 38f, seconds );

		Light( root, pal.Body, 150f, seconds + 0.3f, 0.1f );
	}

	// ─── Briques Raiton (Foudre) ────────────────────────────────────────────

	/// <summary>Noyau électrique et arcs crépitants pour projectile Raiton.</summary>
	public static void LightningOrb( GameObject parent, FxPalette pal, float r )
	{
		var core = Layer( parent, TexFlare4, true, 6, 0, 2, 25, 1, loop: true );
		core.LocalSpace = 1f;
		core.Lifetime = 0.1f;
		core.Scale = new ParticleFloat( r * 1.2f, r * 1.6f );
		core.Gradient = pal.Core;
		core.Brightness = 3f;

		// Arcs électriques crépitants erratiques
		var arcs = Layer( parent, TexLick, true, 20, r * 0.4f, 4, 55, 1, loop: true );
		arcs.Lifetime = new ParticleFloat( 0.08f, 0.16f );
		arcs.Scale = OverLife( r * 1.2f, r * 1.8f );
		arcs.Gradient = ColorOverLife( pal.Body, pal.Dark );
		arcs.Alpha = OverLife( 1f, 0.1f );
		arcs.Brightness = 2.4f;

		// Étincelles rapides
		var sparks = Layer( parent, TexSparkWhite, true, 24, r * 0.3f, 2, 45, 1, loop: true );
		sparks.Lifetime = new ParticleFloat( 0.12f, 0.24f );
		sparks.Scale = OverLife( 6f, 1.5f );
		sparks.StartVelocity = r * 2.5f;
		sparks.Damping = 3f;
		sparks.Gradient = ColorOverLife( pal.Core, pal.Accent );
		sparks.Alpha = OverLife( 1f, 0f );
		sparks.Brightness = 2.8f;
	}

	/// <summary>Étincelles électriques montant le long du corps ou jaillissant.</summary>
	public static ParticleEffect LightningSparks( GameObject parent, FxPalette pal, float radius, float burst, float rate, float duration, float speed, int max = 35 )
	{
		var fx = Layer( parent, TexSparkWhite, true, max, radius, burst, rate, duration );
		fx.Lifetime = new ParticleFloat( 0.18f, 0.42f );
		fx.Scale = OverLife( 7f, 1.5f );
		fx.StartVelocity = new ParticleFloat( speed * 0.5f, speed );
		fx.Damping = 2.5f;
		fx.Force = true;
		fx.ForceDirection = Vector3.Up * 160f;
		fx.Gradient = ColorOverLife( pal.Core, pal.Accent );
		fx.Alpha = OverLife( 1f, 0f );
		fx.Brightness = 3f;
		fx.Components.Get<ParticleSphereEmitter>().Velocity = speed;
		return fx;
	}

	/// <summary>Impact foudre Raiton : flash aveuglant, gerbe d'arcs et étincelles ultra-rapides, rémanence.</summary>
	public static void LightningBurst( GameObject parent, FxPalette pal, float radius )
	{
		// Flash aveuglant
		var flash = Layer( parent, TexWarpBurst, true, 3, 0, 3, 0, 0.1f );
		flash.Lifetime = 0.14f;
		flash.Scale = OverLife( radius * 4f, radius * 7f );
		flash.Gradient = pal.Core;
		flash.Brightness = 3.5f;

		var star = Layer( parent, TexFlare7, true, 2, 0, 2, 0, 0.1f );
		star.Lifetime = 0.12f;
		star.Scale = OverLife( radius * 3f, radius * 5.5f );
		star.Gradient = pal.Accent;
		star.Brightness = 3f;

		// Arcs électriques éclatants
		var arcs = Layer( parent, TexLick, true, 16, radius * 0.3f, 14, 0, 0.1f );
		arcs.Components.Get<ParticleSphereEmitter>().Velocity = radius * 6f;
		arcs.Damping = 4f;
		arcs.Lifetime = new ParticleFloat( 0.16f, 0.32f );
		arcs.Scale = OverLife( radius * 1.4f, radius * 2.4f );
		arcs.Gradient = ColorOverLife( pal.Body, pal.Dark );
		arcs.Alpha = OverLife( 1f, 0f );
		arcs.Brightness = 2.5f;

		// Étincelles projetées à haute vitesse
		var sparks = Layer( parent, TexSparkWhite, true, 32, radius * 0.2f, 28, 0, 0.1f );
		sparks.Components.Get<ParticleSphereEmitter>().Velocity = radius * 10f;
		sparks.Damping = 3.5f;
		sparks.Lifetime = new ParticleFloat( 0.25f, 0.55f );
		sparks.Scale = OverLife( 8f, 1.5f );
		sparks.Gradient = ColorOverLife( pal.Core, pal.Accent );
		sparks.Alpha = OverLife( 1f, 0f );
		sparks.Brightness = 3.2f;

		// Rémanence électrique crépitante au point d'impact
		var linger = Layer( parent, TexSparks, true, 14, radius * 0.6f, 0, 16, 0.9f );
		linger.Lifetime = new ParticleFloat( 0.12f, 0.26f );
		linger.Scale = OverLife( radius * 0.8f, radius * 0.3f );
		linger.Gradient = ColorOverLife( pal.Accent, pal.Body );
		linger.Alpha = OverLife( 0.9f, 0f );
		linger.Brightness = 2.2f;
	}

	/// <summary>Champ électrique de zone : arcs et étincelles au sol sur tout le rayon.</summary>
	public static void LightningField( GameObject parent, FxPalette pal, float radius, float seconds )
	{
		var arcs = Layer( parent, TexLick, true, 20, radius * 0.7f, 6, 16, seconds );
		arcs.Lifetime = new ParticleFloat( 0.1f, 0.22f );
		arcs.Scale = OverLife( radius * 0.4f, radius * 0.9f );
		arcs.Gradient = ColorOverLife( pal.Core, pal.Body );
		arcs.Alpha = OverLife( 1f, 0f );
		arcs.Brightness = 2.8f;
		arcs.Force = true;
		arcs.ForceDirection = Vector3.Up * 120f;

		var sparks = Layer( parent, TexSparkWhite, true, 35, radius * 0.8f, 10, 30, seconds );
		sparks.Lifetime = new ParticleFloat( 0.18f, 0.35f );
		sparks.Scale = OverLife( 7f, 1.5f );
		sparks.Gradient = ColorOverLife( pal.Core, pal.Accent );
		sparks.Alpha = OverLife( 1f, 0f );
		sparks.Brightness = 3f;
		sparks.Components.Get<ParticleSphereEmitter>().Velocity = radius * 3f;
		sparks.Damping = 3f;

		var linger = Layer( parent, TexSparks, true, 18, radius * 0.75f, 4, 18, seconds );
		linger.Lifetime = new ParticleFloat( 0.15f, 0.3f );
		linger.Scale = OverLife( radius * 0.5f, radius * 0.2f );
		linger.Gradient = pal.Accent;
		linger.Brightness = 2.4f;
	}

	/// <summary>Incantation Raiton : arcs crépitants le long du corps, étincelles vives, lumière à fort flicker.</summary>
	public static void RaitonCast( GameObject root, FxPalette pal, float seconds, Vector3 position )
	{
		var flash = Layer( root, TexFlare4, true, 8, 2, 2, 12, seconds );
		flash.Lifetime = 0.12f;
		flash.Scale = OverLife( 18f, 30f );
		flash.Gradient = pal.Core;
		flash.Brightness = 3.5f;

		var arcs = Layer( root, TexLick, true, 22, 18f, 0, 35, seconds );
		arcs.WorldPosition = position + Vector3.Down * 25f;
		arcs.Lifetime = new ParticleFloat( 0.08f, 0.16f );
		arcs.Scale = OverLife( 14f, 22f );
		arcs.Gradient = ColorOverLife( pal.Core, pal.Body );
		arcs.Alpha = OverLife( 1f, 0f );
		arcs.Brightness = 2.8f;

		var sparks = LightningSparks( root, pal, 22f, 6, 45, seconds, 50f, max: 40 );
		sparks.WorldPosition = position + Vector3.Down * 35f;

		var ground = Game.ActiveScene.Trace.Ray( position, position + Vector3.Down * 120f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit ) GroundRing( ground.HitPosition, ground.Normal, pal, 38f, seconds );

		Light( root, pal.Core * 1.5f, 180f, seconds + 0.3f, 0.45f ); // clignotement fort
	}

	// ─── Briques Fûton (Vent) ───────────────────────────────────────────────

	/// <summary>Lame ou croissant d'air comprimé avec tourbillon et feuilles entraînées.</summary>
	public static void WindBlade( GameObject parent, FxPalette pal, float r )
	{
		// Noyau d'air comprimé translucide
		var core = Layer( parent, TexVortex, true, 8, 0, 2, 25, 1, loop: true );
		core.LocalSpace = 1f;
		core.Lifetime = 0.18f;
		core.Scale = new ParticleFloat( r * 1.2f, r * 1.5f );
		core.Gradient = pal.Core;
		core.Alpha = 0.75f;
		core.Brightness = 1.3f;

		// Tourbillon d'air en rotation
		var swirl = Layer( parent, TexSwirl, true, 18, r * 0.45f, 4, 40, 1, loop: true );
		swirl.Lifetime = new ParticleFloat( 0.2f, 0.38f );
		swirl.Scale = OverLife( r * 1.3f, r * 0.5f );
		swirl.Gradient = ColorOverLife( pal.Body, pal.Dark );
		swirl.Alpha = OverLife( 0.8f, 0.1f );
		swirl.Brightness = 1.2f;

		// Traînée vaporeuse blanche
		var wisps = Layer( parent, TexSmokeWispy, false, 12, r * 0.4f, 0, 16, 1, loop: true );
		wisps.Lifetime = new ParticleFloat( 0.35f, 0.6f );
		wisps.Scale = OverLife( r * 0.8f, r * 1.8f );
		wisps.Gradient = pal.Smoke;
		wisps.Alpha = OverLife( 0.4f, 0f );
	}

	/// <summary>Feuilles d'arbres virevoltant dans le vent.</summary>
	public static ParticleEffect Leaves( GameObject parent, FxPalette pal, float radius, float burst, float rate, float duration, float speed, int max = 25 )
	{
		var fx = Layer( parent, TexLeafGreen, false, max, radius, burst, rate, duration );
		fx.Lifetime = new ParticleFloat( 0.6f, 1.2f );
		fx.Scale = OverLife( 7f, 5f );
		fx.StartVelocity = new ParticleFloat( speed * 0.3f, speed );
		fx.Damping = 1.5f;
		fx.Force = true;
		fx.ForceDirection = Vector3.Down * 25f; // planent doucement
		fx.Gradient = pal.Accent;
		fx.Alpha = OverLife( 0.95f, 0f );
		fx.Components.Get<ParticleSphereEmitter>().Velocity = speed;
		return fx;
	}

	/// <summary>Impact de vent Fûton : souffle radial comprimé, onde nette, poussière blanche, pluie de feuilles.</summary>
	public static void WindBurst( GameObject parent, FxPalette pal, float radius )
	{
		// Onde de souffle comprimé
		var shock = Layer( parent, TexWarpBurst, true, 2, 0, 2, 0, 0.1f );
		shock.Lifetime = 0.18f;
		shock.Scale = OverLife( radius * 1.8f, radius * 4.8f );
		shock.Gradient = pal.Core;
		shock.Alpha = OverLife( 0.85f, 0f );

		// Tourbillon d'air radial
		var swirl = Layer( parent, TexSwirl, true, 12, radius * 0.3f, 10, 0, 0.1f );
		swirl.Components.Get<ParticleSphereEmitter>().Velocity = radius * 4.5f;
		swirl.Damping = 3f;
		swirl.Lifetime = new ParticleFloat( 0.35f, 0.6f );
		swirl.Scale = OverLife( radius * 1.2f, radius * 2.8f );
		swirl.Gradient = ColorOverLife( pal.Body, pal.Dark );
		swirl.Alpha = OverLife( 0.75f, 0f );

		// Poussière blanche chassée par le vent
		var dust = Layer( parent, TexSmokeWispyBurst, false, 14, radius * 0.4f, 10, 0, 0.1f );
		dust.Components.Get<ParticleSphereEmitter>().Velocity = radius * 3f;
		dust.Damping = 2f;
		dust.Lifetime = new ParticleFloat( 0.6f, 1.1f );
		dust.Scale = OverLife( radius * 1.2f, radius * 3f );
		dust.Gradient = pal.Smoke;
		dust.Alpha = OverLife( 0.45f, 0f );
		dust.Force = true;
		dust.ForceDirection = Vector3.Up * 20f;

		// Feuilles projetées qui planent et retombent
		var leaves = Layer( parent, TexLeafGreen, false, 22, radius * 0.3f, 18, 0, 0.1f );
		leaves.Components.Get<ParticleSphereEmitter>().Velocity = radius * 5.5f;
		leaves.Damping = 2.5f;
		leaves.Lifetime = new ParticleFloat( 1.1f, 1.8f );
		leaves.Scale = OverLife( 8f, 5.5f );
		leaves.Gradient = pal.Accent;
		leaves.Alpha = OverLife( 1f, 0f );
		leaves.Force = true;
		leaves.ForceDirection = Vector3.Down * 35f;

		var deadLeaves = Layer( parent, TexLeafDead, false, 10, radius * 0.3f, 8, 0, 0.1f );
		deadLeaves.Components.Get<ParticleSphereEmitter>().Velocity = radius * 4.5f;
		deadLeaves.Damping = 2.5f;
		deadLeaves.Lifetime = new ParticleFloat( 1.2f, 1.9f );
		deadLeaves.Scale = OverLife( 7f, 5f );
		deadLeaves.Alpha = OverLife( 0.9f, 0f );
		deadLeaves.Force = true;
		deadLeaves.ForceDirection = Vector3.Down * 35f;
	}

	/// <summary>Incantation Fûton : vortex d'air aspirant, volutes spirales, feuilles soulevées, lumière jade douce.</summary>
	public static void FutonCast( GameObject root, FxPalette pal, float seconds, Vector3 position )
	{
		var core = Layer( root, TexCore, true, 6, 2, 1, 8, seconds );
		core.Lifetime = 0.2f;
		core.Scale = OverLife( 12f, 22f );
		core.Gradient = pal.Core;
		core.Alpha = OverLife( 0.8f, 0f );
		core.Brightness = 1.8f;

		var vortex = Layer( root, TexVortex, true, 12, 16f, 0, 18, seconds );
		vortex.WorldPosition = position + Vector3.Down * 30f;
		vortex.Lifetime = new ParticleFloat( 0.35f, 0.65f );
		vortex.Scale = OverLife( 16f, 28f );
		vortex.Gradient = ColorOverLife( pal.Body, pal.Accent );
		vortex.Alpha = OverLife( 0.7f, 0f );
		vortex.Force = true;
		vortex.ForceDirection = Vector3.Up * 180f;

		var wisps = Layer( root, TexSmokeWispy, false, 12, 18f, 0, 12, seconds );
		wisps.WorldPosition = position + Vector3.Down * 35f;
		wisps.Lifetime = new ParticleFloat( 0.5f, 0.9f );
		wisps.Scale = OverLife( 10f, 24f );
		wisps.Gradient = pal.Smoke;
		wisps.Alpha = OverLife( 0.4f, 0f );
		wisps.Force = true;
		wisps.ForceDirection = Vector3.Up * 200f;

		var leaves = Leaves( root, pal, 24f, 4, 18, seconds, 35f, max: 22 );
		leaves.WorldPosition = position + Vector3.Down * 40f;
		leaves.Force = true;
		leaves.ForceDirection = Vector3.Up * 120f; // montent le long du corps

		var ground = Game.ActiveScene.Trace.Ray( position, position + Vector3.Down * 120f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit ) GroundRing( ground.HitPosition, ground.Normal, pal, 40f, seconds );

		Light( root, pal.Body, 140f, seconds + 0.3f, 0.08f );
	}

	// ─── Briques Doton (Terre) ──────────────────────────────────────────────

	/// <summary>Projectile de roche : amas tellurique dense, éclats rocheux volants, traînée poussiéreuse.</summary>
	public static void RockProjectile( GameObject parent, FxPalette pal, float r )
	{
		// Coeur de terre dense
		var core = Layer( parent, TexNoise, false, 14, r * 0.25f, 2, 25, 1, loop: true );
		core.LocalSpace = 1f;
		core.Lifetime = 0.25f;
		core.Scale = new ParticleFloat( r * 1.1f, r * 1.4f );
		core.Gradient = pal.Body;
		core.Alpha = 0.95f;

		// Éclats de roche gravitant
		var rocks = Layer( parent, TexRockChunks, false, 16, r * 0.5f, 4, 30, 1, loop: true );
		rocks.Lifetime = new ParticleFloat( 0.25f, 0.5f );
		rocks.Scale = OverLife( r * 0.5f, r * 0.7f );
		rocks.Gradient = pal.Dark;
		rocks.Alpha = OverLife( 1f, 0.2f );

		// Poussière ocre en traînée
		var dust = Layer( parent, TexDustBurst, false, 14, r * 0.4f, 0, 18, 1, loop: true );
		dust.Lifetime = new ParticleFloat( 0.45f, 0.8f );
		dust.Scale = OverLife( r * 0.8f, r * 2.2f );
		dust.Gradient = pal.Smoke;
		dust.Alpha = OverLife( 0.6f, 0f );
		dust.Force = true;
		dust.ForceDirection = Vector3.Down * 40f;

		// Gravats éjectés
		var debris = Layer( parent, TexDebris, false, 16, r * 0.3f, 2, 22, 1, loop: true );
		debris.Lifetime = new ParticleFloat( 0.3f, 0.6f );
		debris.Scale = OverLife( 6f, 2f );
		debris.StartVelocity = r * 1.5f;
		debris.Force = true;
		debris.ForceDirection = Vector3.Down * 140f;
		debris.Gradient = pal.Accent;
	}

	/// <summary>Débris et éclats de roche s'élevant ou retombant.</summary>
	public static ParticleEffect RockDebris( GameObject parent, FxPalette pal, float radius, float burst, float rate, float duration, float speed, int max = 30 )
	{
		var fx = Layer( parent, TexRockChunks, false, max, radius, burst, rate, duration );
		fx.Lifetime = new ParticleFloat( 0.5f, 1.0f );
		fx.Scale = OverLife( 7f, 3f );
		fx.StartVelocity = new ParticleFloat( speed * 0.4f, speed );
		fx.Damping = 1.8f;
		fx.Force = true;
		fx.ForceDirection = Vector3.Down * 180f; // gravité
		fx.Gradient = pal.Dark;
		fx.Alpha = OverLife( 1f, 0f );
		fx.Components.Get<ParticleSphereEmitter>().Velocity = speed;
		return fx;
	}

	/// <summary>Impact tellurique Doton : onde de sol, éclats de roche avec gravité, grand nuage de poussière.</summary>
	public static void EarthQuake( GameObject parent, FxPalette pal, float radius )
	{
		// Éclats de roche lourds projetés vers le haut puis retombant
		var rocks = Layer( parent, TexRockChunks, false, 18, radius * 0.3f, 16, 0, 0.1f );
		rocks.Components.Get<ParticleSphereEmitter>().Velocity = radius * 4.5f;
		rocks.Damping = 2f;
		rocks.Force = true;
		rocks.ForceDirection = Vector3.Down * 320f; // pesanteur marquée
		rocks.Lifetime = new ParticleFloat( 0.8f, 1.4f );
		rocks.Scale = OverLife( radius * 0.4f, radius * 0.3f );
		rocks.Gradient = pal.Dark;
		rocks.Alpha = OverLife( 1f, 0f );

		// Béton / cailloux moyens
		var concrete = Layer( parent, TexConcreteChunks, false, 14, radius * 0.3f, 12, 0, 0.1f );
		concrete.Components.Get<ParticleSphereEmitter>().Velocity = radius * 5f;
		concrete.Damping = 2.2f;
		concrete.Force = true;
		concrete.ForceDirection = Vector3.Down * 340f;
		concrete.Lifetime = new ParticleFloat( 0.7f, 1.3f );
		concrete.Scale = OverLife( radius * 0.35f, radius * 0.25f );
		concrete.Gradient = pal.Accent;
		concrete.Alpha = OverLife( 1f, 0f );

		// Petits gravats rapides
		var debris = Layer( parent, TexDebris, false, 24, radius * 0.2f, 20, 0, 0.1f );
		debris.Components.Get<ParticleSphereEmitter>().Velocity = radius * 6.5f;
		debris.Damping = 2.5f;
		debris.Force = true;
		debris.ForceDirection = Vector3.Down * 360f;
		debris.Lifetime = new ParticleFloat( 0.6f, 1.1f );
		debris.Scale = OverLife( 7f, 2.5f );
		debris.Gradient = pal.Accent;
		debris.Alpha = OverLife( 1f, 0f );

		// Nuage de poussière dense ocre/brune qui s'élève
		var dust = Layer( parent, TexDustBurst, false, 18, radius * 0.5f, 10, 4, 1.4f );
		dust.Components.Get<ParticleSphereEmitter>().Velocity = radius * 2.5f;
		dust.Damping = 2f;
		dust.Lifetime = new ParticleFloat( 1.4f, 2.2f );
		dust.Scale = OverLife( radius * 1.2f, radius * 3.4f );
		dust.Gradient = pal.Smoke;
		dust.Alpha = OverLife( 0.7f, 0f );
		dust.Force = true;
		dust.ForceDirection = Vector3.Up * 35f;
	}

	/// <summary>Rupture tellurique pour la zone Doton (Séisme / Poing de pierre).</summary>
	public static void EarthRupture( GameObject parent, FxPalette pal, float radius, float seconds )
	{
		var rocks = Layer( parent, TexRockChunks, false, 24, radius * 0.7f, 10, 16, seconds );
		rocks.Lifetime = new ParticleFloat( 0.8f, 1.5f );
		rocks.Scale = OverLife( radius * 0.35f, radius * 0.25f );
		rocks.Gradient = pal.Dark;
		rocks.Alpha = OverLife( 1f, 0f );
		rocks.Force = true;
		rocks.ForceDirection = Vector3.Down * 300f;
		rocks.Components.Get<ParticleSphereEmitter>().Velocity = radius * 3.5f;
		rocks.Damping = 2f;

		var debris = Layer( parent, TexDebris, false, 28, radius * 0.8f, 12, 22, seconds );
		debris.Lifetime = new ParticleFloat( 0.7f, 1.2f );
		debris.Scale = OverLife( 7f, 3f );
		debris.Gradient = pal.Accent;
		debris.Alpha = OverLife( 1f, 0f );
		debris.Force = true;
		debris.ForceDirection = Vector3.Down * 350f;
		debris.Components.Get<ParticleSphereEmitter>().Velocity = radius * 4.5f;
		debris.Damping = 2.5f;

		var dust = Layer( parent, TexDustBurst, false, 22, radius * 0.8f, 10, 10, seconds );
		dust.Lifetime = new ParticleFloat( 1.5f, 2.5f );
		dust.Scale = OverLife( radius * 0.9f, radius * 2.8f );
		dust.Gradient = pal.Smoke;
		dust.Alpha = OverLife( 0.65f, 0f );
		dust.Force = true;
		dust.ForceDirection = Vector3.Up * 30f;
	}

	/// <summary>Incantation Doton : vibrations du sol, gravats et poussière montant aux pieds, lueur ambre.</summary>
	public static void DotonCast( GameObject root, FxPalette pal, float seconds, Vector3 position )
	{
		var core = Layer( root, TexCore, true, 6, 2, 1, 8, seconds );
		core.Lifetime = 0.2f;
		core.Scale = OverLife( 12f, 20f );
		core.Gradient = pal.Core;
		core.Alpha = OverLife( 0.8f, 0f );
		core.Brightness = 1.6f;

		var debris = RockDebris( root, pal, 22f, 4, 30, seconds, 35f, max: 28 );
		debris.WorldPosition = position + Vector3.Down * 40f;
		debris.Force = true;
		debris.ForceDirection = Vector3.Up * 100f;

		var dust = Layer( root, TexDustBurst, false, 14, 20f, 0, 12, seconds );
		dust.WorldPosition = position + Vector3.Down * 35f;
		dust.Lifetime = new ParticleFloat( 0.6f, 1.1f );
		dust.Scale = OverLife( 12f, 28f );
		dust.Gradient = pal.Smoke;
		dust.Alpha = OverLife( 0.5f, 0f );
		dust.Force = true;
		dust.ForceDirection = Vector3.Up * 50f;

		var ground = Game.ActiveScene.Trace.Ray( position, position + Vector3.Down * 120f ).WithoutTags( Core.NetConstants.TagPlayer ).Run();
		if ( ground.Hit ) GroundRing( ground.HitPosition, ground.Normal, pal, 38f, seconds );

		Light( root, pal.Body, 135f, seconds + 0.3f, 0.12f );
	}
}

/// <summary>Détruit son objet au bout de Seconds (effets temporaires).</summary>
public sealed class JutsuFxTimer : Component
{
	public float Seconds { get; set; } = 1f;
	TimeSince _age;

	protected override void OnStart() => _age = 0;

	protected override void OnUpdate()
	{
		if ( _age >= Seconds ) GameObject.Destroy();
	}
}

/// <summary>Fait scintiller une PointLight et l'éteint doucement sur Seconds (0 = scintille sans s'éteindre).</summary>
public sealed class JutsuFxLight : Component
{
	public float BaseRadius { get; set; } = 200f;
	public Color BaseColor { get; set; } = Color.White;
	public float Seconds { get; set; }
	public float Flicker { get; set; } = 0.15f;

	TimeSince _age;
	float _fadeFrom = -1f, _fadeSeconds;

	protected override void OnStart() => _age = 0;

	/// <summary>Éteint la lumière en <paramref name="seconds"/> à partir de maintenant.</summary>
	public void FadeOut( float seconds )
	{
		_fadeFrom = _age;
		_fadeSeconds = MathF.Max( 0.01f, seconds );
	}

	protected override void OnUpdate()
	{
		var light = GetComponent<PointLight>();
		if ( light is null ) return;

		var k = 1f;
		if ( Seconds > 0f ) k = MathF.Max( 0f, 1f - _age / Seconds );
		if ( _fadeFrom >= 0f ) k *= MathF.Max( 0f, 1f - (_age - _fadeFrom) / _fadeSeconds );
		var flicker = 1f + (Game.Random.Float( -1f, 1f ) * Flicker);

		light.Radius = BaseRadius * MathF.Max( 0.05f, k ) * flicker;
		light.LightColor = BaseColor * (k * flicker);
	}
}

/// <summary>Fait suivre à l'effet d'incantation les déplacements du joueur lanceur.</summary>
public sealed class JutsuFxFollow : Component
{
	public GameObject Target { get; set; }
	public Vector3 Offset { get; set; }

	protected override void OnUpdate()
	{
		if ( !Target.IsValid() )
		{
			GameObject.Destroy();
			return;
		}
		WorldPosition = Target.WorldPosition + Offset;
	}
}
