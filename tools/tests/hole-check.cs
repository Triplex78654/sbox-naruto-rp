// Souterrains par en dessous (Play, hôte = joueur local, perso déjà apparu) :
// 1. chaque sortie X* : le joueur posé au bord du trou et poussé dedans finit sous z = -100 (dans le tunnel) ;
// 2. chaque puits (X*, S*, H1a/H1b) : traces de sphère (r 16, à 20 et 60 u du sol) le long du puits, puis verticale par le trou : libres ;
// 3. le relief fait plafond : depuis le milieu de chaque puits, trace vers le haut -> touche ; joueur lancé vers le haut (900 u/s) -> reste dessous ;
//    pareil depuis chaque salle S1 (archives).
// sbox-cli --call play_start, RequestPlayCharacter, puis run_csharp sur ce fichier. Renvoie "OK ..." ou les échecs. Remet le joueur à sa place.
var scene = Game.ActiveScene;
var p = scene.GetAllComponents<NarutoRp.Player.NinjaPlayer>().FirstOrDefault( n => !n.IsProxy );
if ( p is null ) return "pas de joueur : lancer RequestPlayCharacter d'abord";
var ctrl = p.GetComponent<PlayerController>();
var body = p.GetComponent<Rigidbody>();
var fails = new System.Collections.Generic.List<string>();
var start = p.WorldPosition;
int nPush = 0, nShaft = 0, nUp = 0;
var noPlayer = NarutoRp.Core.NetConstants.TagPlayer;
async System.Threading.Tasks.Task Put( Vector3 pos )
{
	p.WorldPosition = pos; body.Velocity = Vector3.Zero; p.GameObject.Network.ClearInterpolation();
	await Task.Delay( 300 );
}
// Lance le joueur vers le haut (plus fort que le bond) et renvoie la plus haute position atteinte.
async System.Threading.Tasks.Task<float> Launch()
{
	ctrl.PreventGrounding( 0.5f );
	body.Velocity = Vector3.Up * 900f;
	float top = p.WorldPosition.z;
	var sw = System.Diagnostics.Stopwatch.StartNew();
	while ( sw.ElapsedMilliseconds < 1200 ) { top = MathF.Max( top, p.WorldPosition.z ); await Task.Delay( 10 ); }
	return top;
}
// Plafond au-dessus de p : un rayon vers le haut ne touche pas le relief par en dessous (heightfield touché d'un seul côté, vérifié),
// donc rayon vers le bas depuis le ciel : plus bas impact au-dessus de p.
float? Ceiling( Vector3 at )
{
	var hits = scene.Trace.Ray( at.WithZ( 30000f ), at.WithZ( at.z + 10f ) ).WithoutTags( noPlayer ).RunAll().Where( t => t.HitPosition.z > at.z + 10f ).ToList();
	return hits.Count == 0 ? null : hits.Min( t => t.HitPosition.z );
}
var floors = scene.GetAllObjects( true ).Where( g => g.Name.EndsWith( " puits (sol)" ) ).ToList();
if ( floors.Count == 0 ) return "aucun puits dans la scène";
try
{
	foreach ( var f in floors )
	{
		var code = f.Name.Split( ' ' )[0];
		var tag = $"{f.Root.Name.Split( ' ' )[0]} {code}";
		var len = f.WorldScale.x * 50f;
		var fwd = f.WorldRotation.Forward;                       // monte vers la sortie
		var nrm = f.WorldRotation.Up;
		var flat = fwd.WithZ( 0 ).Normal;
		var top = f.WorldPosition + fwd * len / 2f + nrm * 20f;   // haut du sol incliné (dessus de la dalle)
		var bottom = f.WorldPosition - fwd * ( len / 2f - 80f ) + nrm * 20f;
		// 2. le long du puits puis par le trou
		foreach ( var h in new[] { 20f, 60f } )
		{
			var a = bottom + Vector3.Up * h; var b = top - flat * 40f + Vector3.Up * h;
			var tr = scene.Trace.Ray( a, b ).Radius( 16f ).WithoutTags( noPlayer ).Run();
			if ( tr.Hit ) fails.Add( $"{tag} : puits bouché à {tr.HitPosition} par {tr.GameObject?.Name} (h {h})" );
		}
		// jusqu'à 200 u : le joueur (72 u) passe ; l'auvent des entrées S* est à ~255 u, voulu
		var up = scene.Trace.Ray( top - flat * 40f + Vector3.Up * 40f, top - flat * 40f + Vector3.Up * 200f ).Radius( 16f ).WithoutTags( noPlayer ).Run();
		if ( up.Hit ) fails.Add( $"{tag} : trou fermé ({up.GameObject?.Name} à {up.HitPosition.z - top.z:0} u)" );
		nShaft++;
		// 3. plafond : milieu du puits (sous le relief, loin du trou)
		var mid = f.WorldPosition + nrm * 20f - fwd * len * 0.2f;
		var ceil = Ceiling( mid );
		if ( ceil is null ) fails.Add( $"{tag} : rien au-dessus du milieu du puits" );
		else
		{
			await Put( mid + Vector3.Up * 4f );
			var hi = await Launch();
			nUp++;
			if ( hi > ceil.Value ) fails.Add( $"{tag} : joueur passé au-dessus du plafond ({hi:0} > {ceil.Value:0})" );
		}
		// 1. sorties X* : poussé dans le trou depuis le bord (côté palier)
		if ( code[0] == 'X' )
		{
			await Put( top + flat * 120f + Vector3.Up * 10f );
			var sw = System.Diagnostics.Stopwatch.StartNew();
			while ( sw.ElapsedMilliseconds < 5000 && p.WorldPosition.z > -150f )
			{
				body.Velocity = ( -flat * 320f ).WithZ( body.Velocity.z );
				await Task.Delay( 10 );
			}
			await Task.Delay( 300 );
			nPush++;
			if ( p.WorldPosition.z > -100f ) fails.Add( $"{tag} : poussé dans le trou, resté à z {p.WorldPosition.z:0}" );
		}
	}
	// salles S1 (archives) : lancé vers le haut depuis le milieu
	foreach ( var s in scene.GetAllObjects( true ).Where( g => g.Name == "S1 sol" ) )
	{
		var c = s.WorldPosition + Vector3.Up * 24f;
		var ceil = Ceiling( c );
		await Put( c );
		var hi = await Launch();
		nUp++;
		if ( ceil is null || hi > ceil.Value ) fails.Add( $"{s.Root.Name} S1 : plafond franchi ({hi:0})" );
	}
}
finally
{
	await Put( start );
}
return fails.Count == 0 ? $"OK : {nPush} sorties X descendues, {nShaft} puits libres, {nUp} lancers bloqués par le plafond" : string.Join( "\n", fails );
