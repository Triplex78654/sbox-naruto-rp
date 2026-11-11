using Sandbox;
using NarutoRp.Player;
using NarutoRp.Core;

namespace NarutoRp.Shop;

/// <summary>
/// Déclencheur placé sur un PNJ marchand, un comptoir ou une échoppe d'outils shinobi.
/// Permet d'ouvrir le menu d'achat en appuyant sur [E].
/// </summary>
public sealed class ShopTrigger : Component
{
	[Property] public string ShopName { get; set; } = "Échoppe d'outils Shinobi";
	[Property] public float InteractionDistance { get; set; } = 180f;
	[Property] public string PromptText { get; set; } = "Consulter les articles shinobi [E]";

	protected override void OnUpdate()
	{
		var me = NinjaPlayer.Local;
		if ( !me.IsValid() || me.IsProxy ) return;
		if ( ShopState.IsOpen ) return;

		var dist = Vector3.DistanceBetween( WorldPosition, me.WorldPosition );
		if ( dist > InteractionDistance ) return;

		if ( Input.Pressed( "Use" ) && IsLookedAt( me ) )
		{
			ShopState.Open( ShopName );
		}
	}

	bool IsLookedAt( NinjaPlayer me )
	{
		var ctrl = me.GetComponent<PlayerController>();
		if ( ctrl is null ) return false;

		var eye = ctrl.EyePosition;
		var tr = Scene.Trace.Ray( eye, eye + ctrl.EyeAngles.Forward * InteractionDistance )
			.IgnoreGameObjectHierarchy( me.GameObject )
			.Run();

		if ( tr.Hit && tr.GameObject is { } go && (go == GameObject || go.IsDescendant( GameObject )) )
			return true;

		var toShop = (WorldPosition - eye).Normal;
		var dot = Vector3.Dot( ctrl.EyeAngles.Forward, toShop );
		return dot > 0.45f;
	}

	/// <summary>
	/// Traite la transaction d'achat avec validation de l'hôte.
	/// </summary>
	[Rpc.Host]
	public static void PurchaseItemRpc( string itemId )
	{
		var caller = Rpc.Caller;
		var player = Game.ActiveScene?.GetAllComponents<NinjaPlayer>()
			.FirstOrDefault( p => p.Network.Owner == caller );

		if ( !player.IsValid() || player.CharacterId <= 0 ) return;

		// On n'achète que devant un comptoir, et pas de débit sans inventaire pour recevoir l'article.
		var inv = player.GetComponent<NinjaInventory>();
		var nearShop = Game.ActiveScene.GetAllComponents<ShopTrigger>()
			.Any( s => s.WorldPosition.Distance( player.WorldPosition ) <= s.InteractionDistance * 1.5f );
		if ( !inv.IsValid() || !nearShop ) return;

		var item = ShopItemDefinition.DefaultCatalog.FirstOrDefault( x => x.Id == itemId );
		if ( item is null )
		{
			using ( Rpc.FilterInclude( caller ) ) NotifyPurchaseResultRpc( false, "Article inconnu." );
			return;
		}

		if ( player.Ryo < item.PriceRyo )
		{
			using ( Rpc.FilterInclude( caller ) ) NotifyPurchaseResultRpc( false, "Fonds insuffisants en 両 (Ryô) !" );
			return;
		}

		// Déduction des Ryô et ajout des consommables
		player.Ryo -= item.PriceRyo;
		inv.AddItem( item.Id, item.Quantity );

		// Sauvegarde et journalisation
		GameManager.Current?.SaveCharacter( player );
		GameManager.Current?.LogEvent( "buy_item", player.SteamId, null,
			new { item = item.Id, quantity = item.Quantity, price = item.PriceRyo, remainingRyo = player.Ryo } );

		using ( Rpc.FilterInclude( caller ) ) NotifyPurchaseResultRpc( true, $"Achat réussi : +{item.Quantity} {item.Name} !" );
		Log.Info( $"[Boutique] {player.CharacterName} achète {item.Quantity}x {item.Name} pour {item.PriceRyo} Ryô (reste: {player.Ryo})" );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public static void NotifyPurchaseResultRpc( bool success, string message )
	{
		ShopState.SetStatus( message );

		if ( success )
		{
			var snd = ResourceLibrary.Get<SoundEvent>( "sounds/ui/coin.sound" )
				?? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_world.sound" );
			if ( snd is not null )
				Sound.Play( snd );
		}
	}
}
