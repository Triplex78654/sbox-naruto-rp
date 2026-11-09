using NarutoRp.Jutsus;

namespace NarutoRp.Combat;

/// <summary>
/// Effets visuels et sonores des jutsus, créés localement chez chaque client (objets non réseau).
/// Construits en code avec des assets du moteur (sphère + lumière), en attendant de vrais effets.
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

		var go = Create( "Jutsu FX", position, jutsu.FxColor );
		var burst = go.Components.Create<JutsuFxBurst>();
		burst.MaxRadius = MathF.Max( 16f, radius );
	}

	/// <summary>Petite lueur sur le lanceur pendant les signes.</summary>
	public static void Cast( JutsuDefinition jutsu, Vector3 position )
	{
		if ( Disabled || jutsu is null ) return;
		if ( jutsu.CastSound is not null ) Sound.Play( jutsu.CastSound, position );

		var go = Create( "Jutsu FX cast", position, jutsu.FxColor );
		var burst = go.Components.Create<JutsuFxBurst>();
		burst.MaxRadius = 14f;
		burst.Duration = MathF.Max( 0.2f, jutsu.CastSeconds );
	}

	/// <summary>Boule qui avance en ligne droite (le vrai projectile est simulé par l'hôte).</summary>
	public static void Projectile( JutsuDefinition jutsu, Vector3 start, Vector3 direction, GameObject caster )
	{
		if ( Disabled || jutsu is null ) return;

		var go = Create( "Jutsu FX projectile", start, jutsu.FxColor );
		go.WorldScale = MathF.Max( 0.1f, jutsu.HitRadius / 32f * jutsu.FxSize );
		var p = go.Components.Create<JutsuFxProjectile>();
		p.Velocity = direction.Normal * MathF.Max( 1f, jutsu.ProjectileSpeed );
		p.Range = jutsu.Range;
		p.Ignore = caster;
		p.HitRadius = jutsu.HitRadius;
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

		if ( tr.Hit || _travelled >= Range ) { GameObject.Destroy(); return; }
		WorldPosition = next;
	}
}
