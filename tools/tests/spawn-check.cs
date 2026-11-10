// Points d'apparition : chacun libre (boîte 32 x 32 x 72 posée à +10 u) et posé sur le sol (rayon vers le bas, sol à < 30 u).
// 4 points par village (12). Éditeur (hors Play) ou Play : sbox-cli --call run_csharp '{"path":"tools/tests/spawn-check.cs"}'. Renvoie "OK ..." ou les échecs.
var scene = Game.ActiveScene ?? Editor.SceneEditorSession.Active.Scene;
var fails = new System.Collections.Generic.List<string>();
var pts = scene.GetAllComponents<SpawnPoint>().ToList();
foreach ( var v in new[] { "Konoha (blocage)", "Suna (blocage)", "Kumo (blocage)" } )
{
	var n = pts.Count( s => s.GameObject.Root.Name == v );
	if ( n != 4 ) fails.Add( $"{v} : {n} points au lieu de 4" );
}
if ( pts.Count != 12 ) fails.Add( $"{pts.Count} points au total au lieu de 12" );
foreach ( var s in pts )
{
	var pos = s.WorldPosition;
	var tag = $"{s.GameObject.Root.Name} {pos}";
	// les joueurs déjà apparus ne comptent pas comme obstacle
	var box = scene.Trace.Box( new BBox( new Vector3( -16, -16, 0 ), new Vector3( 16, 16, 72 ) ), pos + Vector3.Up * 10f, pos + Vector3.Up * 11f )
		.WithoutTags( NarutoRp.Core.NetConstants.TagPlayer ).Run();
	if ( box.Hit || box.StartedSolid ) fails.Add( $"{tag} : bloqué par {box.GameObject?.Name}" );
	var down = scene.Trace.Ray( pos + Vector3.Up * 30f, pos - Vector3.Up * 30f ).WithoutTags( NarutoRp.Core.NetConstants.TagPlayer ).Run();
	if ( !down.Hit || down.StartedSolid ) fails.Add( $"{tag} : pas de sol à moins de 30 u" );
	else if ( MathF.Abs( down.HitPosition.z - pos.z ) >= 30f ) fails.Add( $"{tag} : sol à {down.HitPosition.z - pos.z:0} u" );
}
return fails.Count == 0 ? $"OK : {pts.Count} points libres et posés" : string.Join( "\n", fails );
