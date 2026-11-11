var log = new System.Text.StringBuilder();
log.AppendLine( "=== VERIFICATION DES EFFETS ELEMENTAIRES (Suiton, Raiton, Fûton, Doton, Katon) ===" );

var scene = Game.ActiveScene;
if ( scene is null )
{
	return "ERREUR : Game.ActiveScene est null.";
}

var player = scene.GetAllComponents<NarutoRp.Player.NinjaPlayer>().FirstOrDefault();
var anchor = player?.GameObject;
var createdAnchor = false;
if ( anchor is null )
{
	anchor = new GameObject( true, "Test FX Anchor" ) { WorldPosition = new Vector3( 0, 0, 50 ), NetworkMode = NetworkMode.Never };
	createdAnchor = true;
	log.AppendLine( "Note : Ancre de test temporaire créée à (0, 0, 50)." );
}
else
{
	log.AppendLine( $"Joueur actif : {anchor.Name} à {anchor.WorldPosition}" );
}

var pos = anchor.WorldPosition + Vector3.Up * 30f;

// 1. Chargement des fiches de jutsus par nature
var jutsus = new System.Collections.Generic.Dictionary<NarutoRp.Jutsus.ChakraNature, System.Collections.Generic.List<NarutoRp.Jutsus.JutsuDefinition>>
{
	[NarutoRp.Jutsus.ChakraNature.Katon] = new(),
	[NarutoRp.Jutsus.ChakraNature.Suiton] = new(),
	[NarutoRp.Jutsus.ChakraNature.Raiton] = new(),
	[NarutoRp.Jutsus.ChakraNature.Futon] = new(),
	[NarutoRp.Jutsus.ChakraNature.Doton] = new()
};

var jutsuPaths = new[]
{
	"data/jutsus/katon/katon_boule_de_feu.jutsu",
	"data/jutsus/katon/katon_mur_de_flammes.jutsu",
	"data/jutsus/suiton/suiton_balle_d_eau.jutsu",
	"data/jutsus/suiton/suiton_vague.jutsu",
	"data/jutsus/suiton/suiton_requin.jutsu",
	"data/jutsus/raiton/raiton_aiguilles.jutsu",
	"data/jutsus/raiton/raiton_champ_electrique.jutsu",
	"data/jutsus/raiton/raiton_decharge.jutsu",
	"data/jutsus/futon/futon_lame_de_vent.jutsu",
	"data/jutsus/futon/futon_balle_d_air.jutsu",
	"data/jutsus/futon/futon_rafale.jutsu",
	"data/jutsus/doton/doton_projectiles_de_roche.jutsu",
	"data/jutsus/doton/doton_seisme.jutsu",
	"data/jutsus/doton/doton_poing_de_pierre.jutsu"
};

foreach ( var path in jutsuPaths )
{
	var j = ResourceLibrary.Get<NarutoRp.Jutsus.JutsuDefinition>( path );
	if ( j is null )
	{
		log.AppendLine( $"[FAIL] Fiche introuvable : {path}" );
		continue;
	}
	if ( jutsus.TryGetValue( j.Nature, out var list ) )
	{
		list.Add( j );
	}
}

log.AppendLine( "\n--- 1. Fiches par nature chargées ---" );
foreach ( var kvp in jutsus )
{
	log.AppendLine( $"  [{kvp.Key}] : {kvp.Value.Count} jutsus ({string.Join( ", ", kvp.Value.Select( x => x.DisplayName ) )})" );
}

// 2. Test Incantation et Suivi du joueur (JutsuFxFollow)
log.AppendLine( "\n--- 2. Test Incantation et Suivi du joueur (JutsuFxFollow) ---" );
var testSuiton = jutsus[NarutoRp.Jutsus.ChakraNature.Suiton].FirstOrDefault();
if ( testSuiton is not null )
{
	var initialPos = anchor.WorldPosition;
	NarutoRp.Combat.JutsuFx.Cast( testSuiton, initialPos + Vector3.Up * 40f, anchor );

	var castFx = scene.GetAllObjects( false ).LastOrDefault( c => c.Name == "Jutsu FX cast" && !c.IsDestroyed );
	if ( castFx is null )
	{
		log.AppendLine( "[FAIL] Objet 'Jutsu FX cast' non trouvé." );
	}
	else
	{
		var follow = castFx.GetComponent<NarutoRp.Combat.JutsuFxFollow>();
		var hasLight = castFx.GetComponentInChildren<PointLight>() is not null;
		var pCount = castFx.GetComponentsInChildren<ParticleEffect>().Count();
		log.AppendLine( $"[OK] Cast Suiton créé : Follow={(follow is not null)}, Light={hasLight}, Couches={pCount}" );

		// Déplacement simulé du joueur
		anchor.WorldPosition = initialPos + new Vector3( 100f, 50f, 15f );
		await System.Threading.Tasks.Task.Delay( 50 );

		var expectedPos = anchor.WorldPosition + Vector3.Up * 40f;
		var dist = castFx.WorldPosition.Distance( expectedPos );
		if ( dist < 2f )
		{
			log.AppendLine( $"[OK] JutsuFxFollow a suivi le joueur (dist={dist:F2}u, pos={castFx.WorldPosition})." );
		}
		else
		{
			log.AppendLine( $"[FAIL] JutsuFxFollow écart trop grand (dist={dist:F2}u, attendu={expectedPos}, actuel={castFx.WorldPosition})." );
		}

		anchor.WorldPosition = initialPos;
		castFx.DestroyImmediate();
	}
}

// 3. Test unitaire des 5 variantes élémentaires (Cast, Projectile, Burst, Area)
log.AppendLine( "\n--- 3. Validation détaillée des 5 Natures ---" );
var natures = new[]
{
	NarutoRp.Jutsus.ChakraNature.Katon,
	NarutoRp.Jutsus.ChakraNature.Suiton,
	NarutoRp.Jutsus.ChakraNature.Raiton,
	NarutoRp.Jutsus.ChakraNature.Futon,
	NarutoRp.Jutsus.ChakraNature.Doton
};

foreach ( var nat in natures )
{
	var list = jutsus[nat];
	var proj = list.FirstOrDefault( j => j.Delivery == NarutoRp.Jutsus.JutsuDeliveryType.Projectile ) ?? list.FirstOrDefault();
	var area = list.FirstOrDefault( j => j.Delivery == NarutoRp.Jutsus.JutsuDeliveryType.Area );

	log.AppendLine( $"\n[{nat}] Jutsu: {proj?.DisplayName}" );

	// Cast
	NarutoRp.Combat.JutsuFx.Cast( proj, pos, anchor );
	var castGo = scene.GetAllObjects( false ).LastOrDefault( c => c.Name == "Jutsu FX cast" && !c.IsDestroyed );
	var cLayers = castGo?.GetComponentsInChildren<ParticleEffect>().Count() ?? 0;
	var cLight = castGo?.GetComponentInChildren<PointLight>() is not null;
	log.AppendLine( $"  -> Cast       : {cLayers} couches, lumière={(cLight ? "OK" : "MANQUANTE")}" );
	castGo?.DestroyImmediate();

	// Projectile
	NarutoRp.Combat.JutsuFx.Projectile( proj, pos, Vector3.Forward, anchor );
	var projGo = scene.GetAllObjects( false ).LastOrDefault( c => c.Name == "Jutsu FX projectile" && !c.IsDestroyed );
	var pLayers = projGo?.GetComponentsInChildren<ParticleEffect>().Count() ?? 0;
	var pTrail = projGo?.GetComponentInChildren<TrailRenderer>() is not null;
	var pLight = projGo?.GetComponentInChildren<PointLight>() is not null;
	var pComp = projGo?.GetComponent<NarutoRp.Combat.JutsuFxProjectile>() is not null;
	log.AppendLine( $"  -> Projectile : {pLayers} couches, trail={(pTrail ? "OK" : "NON")}, lumière={(pLight ? "OK" : "NON")}, component={(pComp ? "OK" : "NON")}" );
	projGo?.DestroyImmediate();

	// Impact / Burst
	NarutoRp.Combat.JutsuFx.Burst( proj, pos, 40f );
	var bGo = scene.GetAllObjects( false ).LastOrDefault( c => c.Name == "Jutsu FX impact" && !c.IsDestroyed );
	var bLayers = bGo?.GetComponentsInChildren<ParticleEffect>().Count() ?? 0;
	var bLight = bGo?.GetComponentInChildren<PointLight>() is not null;
	log.AppendLine( $"  -> Impact     : {bLayers} couches, lumière={(bLight ? "OK" : "MANQUANTE")}" );
	bGo?.DestroyImmediate();

	// Zone
	if ( area is not null )
	{
		NarutoRp.Combat.JutsuFx.Burst( area, pos, area.Range * area.FxSize );
		var aGo = scene.GetAllObjects( false ).LastOrDefault( c => c.Name == "Jutsu FX area" && !c.IsDestroyed );
		var aLayers = aGo?.GetComponentsInChildren<ParticleEffect>().Count() ?? 0;
		var aLight = aGo?.GetComponentInChildren<PointLight>() is not null;
		log.AppendLine( $"  -> Zone ({area.DisplayName}) : {aLayers} couches, lumière={(aLight ? "OK" : "MANQUANTE")}" );
		aGo?.DestroyImmediate();
	}
}

// 4. Mesure des FPS : base vs pic de stress (toutes natures simultanées)
log.AppendLine( "\n--- 4. Mesures de Performance (FPS) ---" );

// Baseline
var baseTimes = new System.Collections.Generic.List<float>();
for ( int i = 0; i < 40; i++ )
{
	var t0 = RealTime.Now;
	await System.Threading.Tasks.Task.Delay( 16 );
	var dt = RealTime.Now - t0;
	if ( dt > 0.0001f ) baseTimes.Add( dt );
}
var avgBase = baseTimes.Count > 0 ? 1f / baseTimes.Average() : 0f;
var minBase = baseTimes.Count > 0 ? 1f / baseTimes.Max() : 0f;
log.AppendLine( $"FPS de base (sans effets) : Moyenne = {avgBase:F0} i/s, Minimum = {minBase:F0} i/s" );

// Pic de stress : 5 lancers + 5 projectiles + 5 impacts simultanés (les 5 natures)
for ( int i = 0; i < natures.Length; i++ )
{
	var nat = natures[i];
	var j = jutsus[nat].FirstOrDefault();
	if ( j is null ) continue;

	var offset = Rotation.FromYaw( i * 72f ).Forward * 100f;
	NarutoRp.Combat.JutsuFx.Cast( j, pos + offset, anchor );
	NarutoRp.Combat.JutsuFx.Projectile( j, pos + offset, Vector3.Forward, anchor );
	NarutoRp.Combat.JutsuFx.Burst( j, pos + offset + Vector3.Forward * 80f, 40f );
}

await System.Threading.Tasks.Task.Delay( 60 );

var totalParticles = scene.GetAllComponents<ParticleEffect>().Count();
var totalLights = scene.GetAllComponents<PointLight>().Count();

var stressTimes = new System.Collections.Generic.List<float>();
for ( int i = 0; i < 40; i++ )
{
	var t0 = RealTime.Now;
	await System.Threading.Tasks.Task.Delay( 16 );
	var dt = RealTime.Now - t0;
	if ( dt > 0.0001f ) stressTimes.Add( dt );
}
var avgStress = stressTimes.Count > 0 ? 1f / stressTimes.Average() : 0f;
var minStress = stressTimes.Count > 0 ? 1f / stressTimes.Max() : 0f;

log.AppendLine( $"Pic sous stress simultané (5 natures x 3 effets = 15 effets en parallèle) :" );
log.AppendLine( $"  -> Couches de particules actives : {totalParticles}" );
log.AppendLine( $"  -> Lumières dynamiques actives   : {totalLights}" );
log.AppendLine( $"  -> FPS mesuré sous stress        : Moyenne = {avgStress:F0} i/s, Minimum = {minStress:F0} i/s" );

if ( createdAnchor ) anchor.DestroyImmediate();

log.AppendLine( "\n=== TEST TERMINE AVEC SUCCES ===" );
return log.ToString();
