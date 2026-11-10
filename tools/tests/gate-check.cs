// Pour chaque village : un joueur (boîte 32x32x72) peut-il marcher de l'intérieur (600 m du coin) à l'extérieur (800 m)
// par l'axe de la grande porte ? Attendu : « passage libre » pour les 3 villages (test Python : tools/test_blocage.py portes_ouvertes).
// Le sol est cherché par une trace verticale partie 300 u au-dessus du sol précédent (comme un joueur qui marche) : une trace
// depuis le ciel tomberait sur le linteau de la porte de Konoha ou le haut du rempart.
var scene = Game.ActiveScene ?? Editor.SceneEditorSession.Active.Scene;
var sb = new System.Text.StringBuilder();
foreach ( var (nom, cx, cy) in new[] { ( "Konoha", -50000f, 50000f ), ( "Suna", 50000f, 50000f ), ( "Kumo", 50000f, -50000f ) } )
{
	var dir = new Vector3( -cx, -cy, 0 ).Normal;
	var corner = new Vector3( cx, cy, 0 );
	float Sol( Vector3 p, float dessus )
	{
		var t = scene.Trace.Ray( p.WithZ( dessus ), p.WithZ( -3000 ) ).Run();
		return t.Hit ? t.EndPosition.z : float.NaN;
	}
	sb.Append( nom + " : " );
	bool bloque = false;
	float sol = Sol( corner + dir * 600 * 40, 20000 );
	for ( float r = 600 * 40; r < 800 * 40; r += 100 )
	{
		var a = corner + dir * r; var b = corner + dir * ( r + 100 );
		a.z = sol + 60; sol = Sol( b, sol + 300 ); b.z = sol + 60;
		var tr = scene.Trace.Box( new BBox( new Vector3( -16, -16, -36 ), new Vector3( 16, 16, 36 ) ), a, b ).Run();
		if ( tr.Hit ) { sb.Append( $"BLOQUÉ à {r / 40:0} m par « {tr.GameObject?.Name} » (parent « {tr.GameObject?.Parent?.Name} »), z sol {a.z - 60:0} ; " ); bloque = true; }
	}
	if ( !bloque ) sb.Append( "passage libre" );
	sb.AppendLine();
}
return sb.ToString();
