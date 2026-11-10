// Test run_csharp de DoorRules : renvoie "OK" ou la liste des échecs.
// sbox-cli --call run_csharp '{"path":"<chemin absolu>"}'
var fails = new System.Collections.Generic.List<string>();
void T( string name, bool expected, NarutoRp.World.DoorRule rule, string doorVillage, string domainClan, string village, string grade, string clan, bool validated, string staff )
{
	var got = NarutoRp.World.DoorRules.CanOpen( rule, doorVillage, domainClan, village, grade, clan, validated, staff );
	if ( got != expected ) fails.Add( $"{name} : attendu {expected}, obtenu {got}" );
}
T( "kage meme village + Council", true, NarutoRp.World.DoorRule.Council, "konoha", null, "konoha", "kage", null, false, null );
T( "kage autre village", false, NarutoRp.World.DoorRule.Kage, "konoha", null, "suna", "kage", null, false, null );
T( "jonin + Police", false, NarutoRp.World.DoorRule.Police, "konoha", null, "konoha", "jonin", null, false, null );
T( "staff moderator + Clan sans proprietaire", true, NarutoRp.World.DoorRule.Clan, "konoha", null, "suna", "genin", null, false, "moderator" );
T( "clan egal non valide", false, NarutoRp.World.DoorRule.Clan, "konoha", "uchiwa", "konoha", "genin", "uchiwa", false, null );
T( "clan egal valide meme village", true, NarutoRp.World.DoorRule.Clan, "konoha", "uchiwa", "konoha", "genin", "uchiwa", true, null );
T( "clan egal valide autre village", false, NarutoRp.World.DoorRule.Clan, "konoha", "uchiwa", "suna", "genin", "uchiwa", true, null );
T( "Clan domaine vide non staff", false, NarutoRp.World.DoorRule.Clan, "konoha", "", "konoha", "genin", "", true, null );
T( "Staff seul : kage refuse", false, NarutoRp.World.DoorRule.Staff, "konoha", null, "konoha", "kage", null, false, null );
T( "Staff seul : staff ok", true, NarutoRp.World.DoorRule.Staff, "konoha", null, "konoha", "genin", null, false, "admin" );
return fails.Count == 0 ? "OK" : string.Join( "\n", fails );
