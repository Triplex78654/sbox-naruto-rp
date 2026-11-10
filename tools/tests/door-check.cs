// Test en Play (hôte = joueur local, perso déjà spawné) : crée une porte TEST devant le joueur, ne sauvegarde rien, détruit tout à la fin.
// sbox-cli --call play_start puis run_csharp sur ce fichier. Renvoie "OK" ou la liste des échecs.
var scene = Game.ActiveScene;
var p = scene.GetAllComponents<NarutoRp.Player.NinjaPlayer>().FirstOrDefault( n => !n.IsProxy );
if ( p is null ) return "pas de joueur : lancer RequestPlayCharacter d'abord";
var fails = new System.Collections.Generic.List<string>();
var oldStaff = p.StaffRank; var oldGrade = p.Grade; var oldVillage = p.Village;
var ctrl = p.GetComponent<PlayerController>();
var fwd = Rotation.FromYaw( ctrl.EyeAngles.yaw ).Forward;
var door = new GameObject( true, "Porte TEST" );
door.WorldPosition = p.WorldPosition + fwd * 120f;
door.WorldRotation = Rotation.LookAt( -fwd, Vector3.Up ); // l'avant pointe vers le joueur
var lk = door.Components.Create<NarutoRp.World.LockedDoor>();
lk.Rule = NarutoRp.World.DoorRule.Kage; lk.Village = "konoha";
var leaf = new GameObject( door, true, "Battant" );
leaf.LocalPosition = new Vector3( 0, 0, 100 );
leaf.LocalScale = new Vector3( 20f / 50f, 600f / 50f, 200f / 50f );
leaf.Components.Create<ModelRenderer>().Model = Model.Load( "models/dev/box.vmdl" );
leaf.Components.Create<BoxCollider>().Scale = new Vector3( 50, 50, 50 );
try
{
	await Task.Delay( 300 );
	// 1. joueur non autorisé (pas staff, pas kage) : reste fermée.
	p.StaffRank = null; p.Grade = "genin"; p.Village = "konoha";
	lk.RequestOpen(); await Task.Delay( 300 );
	if ( lk.IsOpen ) fails.Add( "non autorisé a ouvert" );
	// 2. Kage d'un autre village : refusé ; du bon village : ouvert.
	p.Grade = "kage"; p.Village = "suna"; lk.RequestOpen(); await Task.Delay( 300 );
	if ( lk.IsOpen ) fails.Add( "kage autre village a ouvert" );
	p.Village = "konoha"; lk.RequestOpen(); await Task.Delay( 300 );
	if ( !lk.IsOpen ) fails.Add( "kage du village refusé" );
	if ( leaf.Enabled ) fails.Add( "battant actif porte ouverte" );
	await Task.Delay( 4500 );
	if ( lk.IsOpen ) fails.Add( "pas refermée après 4 s" );
	if ( !leaf.Enabled ) fails.Add( "battant inactif porte fermée" );
	// 3. staff : ouvre.
	p.StaffRank = "moderator"; p.Grade = "genin"; p.Village = "suna";
	lk.RequestOpen(); await Task.Delay( 300 );
	if ( !lk.IsOpen ) fails.Add( "staff refusé" );
	await Task.Delay( 4500 );
	if ( lk.IsOpen ) fails.Add( "staff : pas refermée" );
	// 4. sens unique : bon côté (devant) accepté, mauvais côté refusé.
	lk.OneWay = true;
	var start = p.WorldPosition;
	p.WorldPosition = door.WorldPosition + fwd * 100f; await Task.Delay( 200 ); // derrière la porte (côté opposé à son avant)
	// Le staff passe par-dessus le sens unique (il ouvre tout) ; un Kage du village, non.
	lk.RequestOpen(); await Task.Delay( 300 );
	if ( !lk.IsOpen ) fails.Add( "OneWay : staff refusé du mauvais côté" );
	await Task.Delay( 4500 );
	p.StaffRank = null; p.Grade = "kage"; p.Village = "konoha";
	lk.RequestOpen(); await Task.Delay( 300 );
	if ( lk.IsOpen ) fails.Add( "OneWay : mauvais côté accepté" );
	p.WorldPosition = start; await Task.Delay( 200 );
	lk.RequestOpen(); await Task.Delay( 300 );
	if ( !lk.IsOpen ) fails.Add( "OneWay : bon côté refusé" );
	await Task.Delay( 4500 );
	// 5. distance > 200 refusée.
	p.WorldPosition = start + ( -fwd ) * 150f; await Task.Delay( 200 );
	lk.RequestOpen(); await Task.Delay( 300 );
	if ( lk.IsOpen ) fails.Add( "distance > 200 acceptée" );
	p.WorldPosition = start; await Task.Delay( 200 );
	// 6. Shunshin porte fermée : la destination s'arrête avant la porte.
	lk.OneWay = false;
	var caster = p.GetComponent<NarutoRp.Combat.JutsuCaster>();
	var jut = ResourceLibrary.GetAll<NarutoRp.Jutsus.JutsuDefinition>().FirstOrDefault( j => j.Delivery == NarutoRp.Jutsus.JutsuDeliveryType.Movement );
	if ( jut is null ) fails.Add( "aucun jutsu Shunshin trouvé" );
	else
	{
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var t = typeof( NarutoRp.Combat.JutsuCaster );
		t.GetField( "_castAim", flags ).SetValue( caster, fwd );
		var args = new object[] { jut, null };
		var ok = (bool)t.GetMethod( "TryGetShunshinDestination", flags ).Invoke( caster, args );
		var dest = (Vector3)args[1];
		var before = Vector3.Dot( p.WorldPosition - door.WorldPosition, fwd );
		var after = Vector3.Dot( dest - door.WorldPosition, fwd );
		if ( ok && Math.Sign( before ) != Math.Sign( after ) ) fails.Add( $"Shunshin traverse la porte fermée (range {jut.Range}, avant {before}, après {after})" );
		if ( !ok ) fails.Add( "Shunshin sans destination (test non concluant)" );
		// Témoin : porte ouverte, le Shunshin la traverse.
		lk.RequestOpen(); await Task.Delay( 300 );
		var args2 = new object[] { jut, null };
		t.GetMethod( "TryGetShunshinDestination", flags ).Invoke( caster, args2 );
		var after2 = Vector3.Dot( (Vector3)args2[1] - door.WorldPosition, fwd );
		if ( lk.IsOpen && Math.Sign( before ) == Math.Sign( after2 ) ) fails.Add( "témoin : porte ouverte mais Shunshin arrêté quand même" );
	}
}
finally
{
	door.Destroy();
	p.StaffRank = oldStaff; p.Grade = oldGrade; p.Village = oldVillage;
}
return fails.Count == 0 ? "OK" : string.Join( "\n", fails );
