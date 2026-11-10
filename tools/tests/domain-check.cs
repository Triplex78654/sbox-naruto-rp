// Test en Play (hôte = joueur local, perso déjà spawné, API mémoire) : porte de clan TEST, ne sauvegarde rien, détruite à la fin.
// sbox-cli --call play_start puis run_csharp sur ce fichier. Renvoie "OK" ou la liste des échecs.
var scene = Game.ActiveScene;
var p = scene.GetAllComponents<NarutoRp.Player.NinjaPlayer>().FirstOrDefault( n => !n.IsProxy );
if ( p is null ) return "pas de joueur : lancer RequestPlayCharacter d'abord";
var fails = new System.Collections.Generic.List<string>();
var oldStaff = p.StaffRank; var oldGrade = p.Grade; var oldVillage = p.Village; var oldClan = p.Clan; var oldVal = p.ClanValidated;
var cmd = p.GetComponent<NarutoRp.Staff.StaffCommands>();
var door = new GameObject( true, "Refuge TEST" );
door.WorldPosition = p.WorldPosition + Vector3.Forward * 60f;
var lk = door.Components.Create<NarutoRp.World.LockedDoor>();
lk.Rule = NarutoRp.World.DoorRule.Clan; lk.Village = "konoha"; lk.DomainId = "konoha-TEST";
async System.Threading.Tasks.Task<bool> TryOpen()
{
	lk.RequestOpen(); await Task.Delay( 300 );
	var o = lk.IsOpen;
	if ( o ) await Task.Delay( 4500 );
	return o;
}
try
{
	await Task.Delay( 300 );
	p.Clan = "Hanabi"; p.ClanValidated = true; p.Village = "konoha"; p.Grade = "genin";
	// Staff pour tester la commande (admin dans l'API mémoire), mais la porte doit être testée en simple joueur.
	p.StaffRank = "admin";
	cmd.RequestSetDomainClan( "konoha-TEST", "konoha", "Hanabi" ); await Task.Delay( 800 );
	if ( lk.OwnerClan != "Hanabi" ) fails.Add( $"OwnerClan après attribution = {lk.OwnerClan}" );
	var api = NarutoRp.Core.GameManager.Current.Api;
	var list = await api.GetDomainsAsync();
	if ( !list.Any( d => d.Id == "konoha-TEST" && d.Clan == "Hanabi" ) ) fails.Add( "API sans le domaine attribué" );
	p.StaffRank = null;
	if ( !await TryOpen() ) fails.Add( "membre du clan refusé" );
	p.Clan = "Autre";
	if ( await TryOpen() ) fails.Add( "autre clan accepté" );
	p.Clan = "Hanabi"; p.ClanValidated = false;
	if ( await TryOpen() ) fails.Add( "clan non validé accepté" );
	p.ClanValidated = true; p.Village = "suna";
	if ( await TryOpen() ) fails.Add( "autre village accepté" );
	p.Village = "konoha";
	// Reconnexion simulée : la porte reprend son clan depuis le registre.
	NarutoRp.World.DomainRegistry.Set( "konoha-TEST", "Hanabi" );
	if ( lk.OwnerClan != "Hanabi" ) fails.Add( "registre → porte" );
	// Retrait.
	p.StaffRank = "admin";
	cmd.RequestSetDomainClan( "konoha-TEST", "konoha", null ); await Task.Delay( 800 );
	p.StaffRank = null;
	if ( lk.OwnerClan != null ) fails.Add( $"OwnerClan après retrait = {lk.OwnerClan}" );
	if ( await TryOpen() ) fails.Add( "ouvert après retrait" );
	// Id d'un autre village refusé côté commande.
	p.StaffRank = "admin";
	cmd.RequestSetDomainClan( "suna-TEST", "konoha", "Hanabi" ); await Task.Delay( 800 );
	if ( NarutoRp.World.DomainRegistry.ClanOf( "suna-TEST" ) != null ) fails.Add( "id d'un autre village accepté" );
}
finally
{
	NarutoRp.World.DomainRegistry.Set( "konoha-TEST", null );
	door.Destroy();
	p.StaffRank = oldStaff; p.Grade = oldGrade; p.Village = oldVillage; p.Clan = oldClan; p.ClanValidated = oldVal;
}
return fails.Count == 0 ? "OK" : string.Join( "\n", fails );
