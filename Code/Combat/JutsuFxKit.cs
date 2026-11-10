namespace NarutoRp.Combat;

/// <summary>
/// Couleurs d'un effet élémentaire : coeur brillant, corps, bords sombres et fumée.
/// Tirée de la couleur de la fiche (FxColor) : le même kit sert au feu, à l'eau, à la foudre...
/// </summary>
public readonly record struct FxPalette( Color Core, Color Body, Color Dark, Color Smoke )
{
	public static FxPalette From( Color c ) => new(
		new Color( Mix( c.r, 1f, 0.75f ), Mix( c.g, 1f, 0.6f ), Mix( c.b, 1f, 0.35f ) ), // vers le blanc-jaune
		c,
		new Color( c.r * 0.7f, c.g * 0.25f, c.b * 0.2f ), // rouge sombre pour le feu
		new Color( 0.12f, 0.1f, 0.09f ) );

	static float Mix( float a, float b, float t ) => a + (b - a) * t;
}

/// <summary>
/// Briques d'effets visuels (client seulement, objets non réseau, aucune logique de jeu) :
/// boule de flammes, traînée, braises, flash, sphère d'explosion, onde au sol, fumée, lumière,
/// brûlure au sol et tremblement de caméra (CameraEffectSystem.AddShake). Chaque brique prend une palette et une taille,
/// pour assembler plus tard les variantes Suiton / Raiton / Fûton / Doton.
/// Budget : chaque couche a un MaxParticles bas (une boule de feu complète reste sous ~200 sprites).
/// </summary>
public static class JutsuFxKit
{
	// Textures fournies avec le moteur (core/textures/particles).
	const string TexGlow = "textures/particles/particle_glow_01.vtex";
	const string TexCore = "textures/particles/particle_glow_07.vtex";
	const string TexFlame = "textures/particles/particle_noisesphere_color.vtex";
	const string TexLick = "textures/particles/muzzleflash/muzzleflash.vtex";
	const string TexSmoke = "textures/particles/particle_noisesphere_color.vtex";
	const string TexEmber = "textures/particles/flame/ember.vtex";
	const string TexRing = "textures/particles/particle_ring_wave.vtex";
	const string ScorchDecal = "decals/scorchmarks/decal_scorchmark_01.decal";

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
	/// Les tailles et vitesses se règlent ensuite sur l'effet renvoyé.
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

	static ParticleGradient ColorOverLife( Color a, Color b ) => new()
	{
		Type = ParticleGradient.ValueType.Range,
		Evaluation = ParticleGradient.EvaluationType.Life,
		ConstantA = a,
		ConstantB = b,
	};

	// ─── Briques ─────────────────────────────────────────────────────────────

	/// <summary>Boule de flammes en couches : coeur blanc-jaune, corps orange, langues rouge sombre, volutes de fumée.
	/// Les couches du corps sont en espace monde : en vol, elles restent derrière et font la traînée de chaleur.</summary>
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
		body.Roll = new ParticleFloat( 0, 360 ) { Evaluation = ParticleFloat.EvaluationType.Seed };

		// Langues non additives : le rouge sombre reste lisible par-dessus le coeur brillant.
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

	/// <summary>Traînée lumineuse qui s'amincit derrière l'objet qui bouge.</summary>
	public static void Trail( GameObject parent, FxPalette pal, float width )
	{
		var go = new GameObject( parent, true, "fx trail" ) { NetworkMode = NetworkMode.Never };
		var t = go.Components.Create<TrailRenderer>();
		t.LifeTime = 0.3f;
		t.PointDistance = 6f;
		t.MaxPoints = 32;
		t.Opaque = false;
		t.BlendMode = BlendMode.Lighten;

		Curve w = width;
		w.AddPoint( 0f, width );
		w.AddPoint( 1f, 0f );
		t.Width = w;

		Gradient g = pal.Core;
		g.AddColor( 0.3f, pal.Body );
		g.AddColor( 1f, pal.Dark );
		g.AddAlpha( 0f, 0.6f );
		g.AddAlpha( 1f, 0f );
		t.Color = g;
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

	/// <summary>Explosion : flash, sphère de feu qui grossit, langues, braises, fumée et flammes qui traînent ~1,5 s.</summary>
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

		// Flammes qui restent au sol après l'explosion.
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

	/// <summary>Onde de choc à plat sur le sol (anneau qui s'élargit et s'efface). Sol supposé à peu près plat.</summary>
	public static void GroundRing( Vector3 ground, Vector3 normal, FxPalette pal, float radius, float seconds = 0.5f )
	{
		var root = Root( "fx ring", ground + normal * 3f, seconds + 0.2f );
		var ring = Layer( root, TexRing, true, 2, 0, 1, 0, 0.1f );
		// Alignement Particle + tangage 90 = sprite couché à plat (vérifié en jeu ; l'alignement Object reste face caméra).
		ring.Components.Get<ParticleSpriteRenderer>().Alignment = ParticleSpriteRenderer.BillboardAlignment.Particle;
		ring.Pitch = 90f;
		ring.Roll = 0f;
		ring.Lifetime = seconds;
		ring.Scale = OverLife( radius * 0.3f, radius * 2.2f );
		ring.Gradient = ColorOverLife( pal.Core, pal.Body );
		ring.Alpha = OverLife( 1f, 0f );
		ring.Brightness = 1.2f;
	}

	/// <summary>Trace de brûlure sur le sol, qui s'efface au bout de <paramref name="seconds"/>.</summary>
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

	/// <summary>Tremblement de caméra pour les joueurs proches : plein à <paramref name="position"/>, nul à <paramref name="radius"/>.</summary>
	public static void Shake( Vector3 position, float amplitude, float radius, float seconds = 0.45f )
	{
		Game.ActiveScene.GetSystem<CameraEffectSystem>()?.AddShake( position, radius, amplitude, 25f, seconds );
	}

	/// <summary>Coupe l'émission (boule qui touche) et laisse les particules déjà lancées finir leur vie.</summary>
	public static void Release( GameObject root, float seconds = 1f )
	{
		foreach ( var e in root.Components.GetAll<ParticleEmitter>( FindMode.EverythingInSelfAndDescendants ) ) e.Enabled = false;
		foreach ( var t in root.Components.GetAll<TrailRenderer>( FindMode.EverythingInSelfAndDescendants ) ) t.Emitting = false;
		foreach ( var l in root.Components.GetAll<JutsuFxLight>( FindMode.EverythingInSelfAndDescendants ) ) l.FadeOut( 0.15f );
		root.Components.GetOrCreate<JutsuFxTimer>().Seconds = seconds;
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
