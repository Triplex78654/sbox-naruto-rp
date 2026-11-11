namespace NarutoRp.UI;

/// <summary>
/// État client du panneau / bureau des missions shinobi.
/// </summary>
public static class MissionBoardState
{
	/// <summary>Indique si l'interface du tableau des missions est ouverte.</summary>
	public static bool IsOpen { get; set; }

	/// <summary>Village du bureau consulté ("Konoha", "Suna", "Kumo" ou "Tous").</summary>
	public static string CurrentVillage { get; set; } = "Konoha";

	/// <summary>Ouvre l'interface pour un village spécifique.</summary>
	public static void OpenMenu( string village = "Konoha" )
	{
		CurrentVillage = village ?? "Konoha";
		IsOpen = true;
	}

	/// <summary>Ferme l'interface du bureau des missions.</summary>
	public static void Close()
	{
		IsOpen = false;
	}
}
