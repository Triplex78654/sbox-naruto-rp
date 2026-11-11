using Sandbox;
using NarutoRp.Player;
using NarutoRp.Core;

namespace NarutoRp.Shop;

/// <summary>
/// État et gestion des transactions de la boutique marchand côté client et serveur.
/// </summary>
public static class ShopState
{
	/// <summary>Indique si l'interface de la boutique est ouverte.</summary>
	public static bool IsOpen { get; set; }

	/// <summary>Nom ou enseigne de l'échoppe consultée.</summary>
	public static string CurrentShopName { get; set; } = "Échoppe d'outils Shinobi";

	/// <summary>Catalogue actuel des articles en vente.</summary>
	public static List<ShopItemDefinition> CurrentCatalog { get; set; } = ShopItemDefinition.DefaultCatalog;

	/// <summary>Message d'état temporaire (achat réussi, fonds insuffisants...).</summary>
	public static string StatusMessage { get; set; }
	public static TimeSince TimeSinceStatus { get; set; } = 10f;

	public static void Open( string shopName = "Échoppe d'outils Shinobi" )
	{
		CurrentShopName = shopName;
		CurrentCatalog = ShopItemDefinition.DefaultCatalog;
		IsOpen = true;
		StatusMessage = null;
	}

	public static void Close()
	{
		IsOpen = false;
		StatusMessage = null;
	}

	public static void SetStatus( string message )
	{
		StatusMessage = message;
		TimeSinceStatus = 0f;
	}
}
