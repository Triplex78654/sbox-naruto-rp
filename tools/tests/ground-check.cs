// Contenu des territoires posé au sol (Play ou éditeur) : pour chaque volume des groupes « … (territoire) » (routes, postes,
// arches, forêts, contenu replacé du Pays du Feu, des terres neutres et du centre commun), une trace vers le bas depuis son
// point le plus bas + 200 u touche le sol (relief ou autre volume) à <= 40 u de ce point (rien ne flotte), et le dessus de
// l'objet est au-dessus du relief (rien n'est entièrement enterré).
// Exclus (pas posés au sol par construction) : eau, cascades, ponts, feuillages, toits, linteaux et sommets d'arches.
// sbox-cli --call run_csharp '{"path":".../tools/tests/ground-check.cs"}'. Renvoie "OK ..." ou les échecs (30 premiers).
var scene = Game.IsPlaying ? Game.ActiveScene : Editor.SceneEditorSession.Active.Scene;
var relief = scene.GetAllObjects( true ).FirstOrDefault( g => g.Name == "Relief" );
if ( relief is null ) return "pas de Relief dans la scène";
string[] sansSol = { "Feuillage", "(toit)", "(linteau)", "(sommet)", "Chute", "Bassin" };
var groupes = scene.Children.Where( g => g.Name.EndsWith( " (territoire)" ) ).ToList();
if ( groupes.Count == 0 ) return "aucun groupe (territoire)";
var fails = new System.Collections.Generic.List<string>();
int n = 0;
foreach ( var grp in groupes )
	foreach ( var go in grp.GetAllObjects( true ) )
	{
		if ( go.Components.Get<ModelRenderer>() is not ModelRenderer mr ) continue;
		if ( go.Parent?.Name is "Eau" or "Cascades" or "Ponts" || sansSol.Any( s => go.Name.Contains( s ) ) ) continue;
		float zMin, zMax;
		if ( mr.Model.ResourcePath == "models/dev/box.vmdl" )
		{
			// cube de 50 u centré : coins exacts
			var zs = new[] { -25f, 25f }.SelectMany( x => new[] { -25f, 25f }.SelectMany( y => new[] { -25f, 25f }.Select( z => go.WorldTransform.PointToWorld( new Vector3( x, y, z ) ).z ) ) ).ToList();
			zMin = zs.Min(); zMax = zs.Max();
		}
		else { var b = go.GetBounds(); zMin = b.Mins.z; zMax = b.Maxs.z; }
		var c = go.WorldPosition;
		n++;
		var tr = scene.Trace.Ray( c.WithZ( zMin + 200f ), c.WithZ( zMin - 2000f ) ).IgnoreGameObjectHierarchy( go ).Run();
		var nom = $"{grp.Name} / {go.Parent?.Name} / {go.Name} à ({c.x:0}, {c.y:0})";
		if ( !tr.Hit ) { fails.Add( $"{nom} : pas de sol sous l'objet" ); continue; }
		if ( zMin - tr.HitPosition.z > 40f ) fails.Add( $"{nom} : flotte à {zMin - tr.HitPosition.z:0} u du sol" );
		// relief seul au centre, trace depuis le ciel (le heightfield ne se touche que par le dessus)
		var sol = scene.Trace.Ray( c.WithZ( 30000f ), c.WithZ( -30000f ) ).IgnoreGameObjectHierarchy( go ).RunAll().FirstOrDefault( t => t.GameObject == relief );
		if ( sol.Hit && zMax < sol.HitPosition.z ) fails.Add( $"{nom} : entièrement sous le relief ({zMax:0} < {sol.HitPosition.z:0})" );
	}
return fails.Count == 0 ? $"OK : {n} objets posés au sol dans {groupes.Count} territoires" : $"{fails.Count} échecs / {n} :\n" + string.Join( "\n", fails.Take( 30 ) );
