using System.Threading.Tasks;
using NarutoRp.Network.Api;

namespace NarutoRp.World;

/// <summary>
/// Domaines de clan (hôte seulement) : id du domaine → clan propriétaire, venant de la base.
/// Chaque mise à jour recopie le clan dans OwnerClan des portes concernées (synchronisé aux clients).
/// </summary>
public static class DomainRegistry
{
	static readonly Dictionary<string, string> Clans = new();

	public static string ClanOf( string domainId )
		=> !string.IsNullOrEmpty( domainId ) && Clans.TryGetValue( domainId, out var clan ) ? clan : null;

	public static void Set( string domainId, string clan )
	{
		if ( string.IsNullOrEmpty( domainId ) ) return;
		if ( string.IsNullOrEmpty( clan ) ) Clans.Remove( domainId ); else Clans[domainId] = clan;
		Apply();
	}

	/// <summary>Charge la liste depuis l'API. Échec : journalisé, domaines vides (seuls staff et Kage entrent).</summary>
	public static async Task LoadAsync( IApiClient api )
	{
		try
		{
			var list = await api.GetDomainsAsync();
			Clans.Clear();
			foreach ( var d in list )
				if ( !string.IsNullOrEmpty( d.Id ) && !string.IsNullOrEmpty( d.Clan ) ) Clans[d.Id] = d.Clan;
		}
		catch ( Exception e )
		{
			Clans.Clear();
			Log.Warning( $"[Domaines] chargement impossible, domaines vides : {e.Message}" );
		}
		Apply();
	}

	static void Apply()
	{
		var scene = Game.ActiveScene;
		if ( scene is null ) return;
		foreach ( var door in scene.GetAllComponents<LockedDoor>() )
			door.OwnerClan = ClanOf( door.DomainId );
	}
}
