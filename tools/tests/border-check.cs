// Bord de la carte : depuis 40 points au bord intérieur de chaque côté (z = sol + 100 et sol + 3000),
// une trace vers l'extérieur sur 8 000 u touche toujours un collider avant |x| ou |y| = HALF (56 000).
// Éditeur ou Play : sbox-cli --call run_csharp '{"path":"tools/tests/border-check.cs"}'. Renvoie "OK ..." ou les échecs.
var scene = Game.ActiveScene ?? Editor.SceneEditorSession.Active.Scene;
const float Half = 56000f, Inner = 50000f;
var fails = new System.Collections.Generic.List<string>();
int n = 0;
foreach ( var (nom, dir) in new[] { ( "nord", new Vector3( 0, 1, 0 ) ), ( "sud", new Vector3( 0, -1, 0 ) ), ( "est", new Vector3( 1, 0, 0 ) ), ( "ouest", new Vector3( -1, 0, 0 ) ) } )
{
	var side = new Vector3( dir.y, -dir.x, 0 );   // le long du bord
	for ( int i = 0; i < 40; i++ )
	{
		var t = -Inner + 1000f + i * ( 2 * Inner - 2000f ) / 39f;
		var p = dir * ( Inner - 1000f ) + side * t;
		var ground = scene.Trace.Ray( p.WithZ( 30000f ), p.WithZ( -5000f ) ).Run();
		if ( !ground.Hit ) { fails.Add( $"{nom} {i} : pas de sol sous {p}" ); continue; }
		foreach ( var dz in new[] { 100f, 3000f } )
		{
			var a = p.WithZ( ground.HitPosition.z + dz );
			var tr = scene.Trace.Ray( a, a + dir * 8000f ).Run();
			n++;
			var reach = MathF.Abs( Vector3.Dot( tr.HitPosition, dir ) );
			if ( !tr.Hit || reach >= Half ) fails.Add( $"{nom} {i} z+{dz} : sortie possible ({a})" );
		}
	}
}
return fails.Count == 0 ? $"OK : {n} traces arrêtées avant le bord" : string.Join( "\n", fails );
