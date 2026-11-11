using Sandbox;
using NarutoRp.Core;
using NarutoRp.Combat;

namespace NarutoRp.Player;

/// <summary>
/// Inventaire des consommables et outils ninja du joueur (Kunais, Shurikens,
/// Parchemins explosifs, Fumigènes, Pilules du soldat).
/// Synchronisé et validé par l'hôte.
/// </summary>
public sealed class NinjaInventory : Component
{
	[RequireComponent] NinjaPlayer Player { get; set; }
	[RequireComponent] NinjaStats Stats { get; set; }

	/// <summary>Stock de kunais disponibles.</summary>
	[Sync( SyncFlags.FromHost )] public int KunaiCount { get; set; } = 15;

	/// <summary>Stock de shurikens disponibles.</summary>
	[Sync( SyncFlags.FromHost )] public int ShurikenCount { get; set; } = 25;

	/// <summary>Stock de parchemins explosifs (Kibaku Fuda).</summary>
	[Sync( SyncFlags.FromHost )] public int ExplosiveTagCount { get; set; } = 4;

	/// <summary>Stock de bombes fumigènes (Kemuridama).</summary>
	[Sync( SyncFlags.FromHost )] public int SmokeBombCount { get; set; } = 3;

	/// <summary>Stock de pilules militaires du soldat (Hyôryôgan).</summary>
	[Sync( SyncFlags.FromHost )] public int SoldierPillCount { get; set; } = 2;

	/// <summary>Temps restant avant de pouvoir reprendre une pilule du soldat (cooldown).</summary>
	[Sync( SyncFlags.FromHost )] public float SoldierPillCooldown { get; private set; }

	public const float MaxSoldierPillCooldown = 15f;

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsHost && SoldierPillCooldown > 0 )
			SoldierPillCooldown = MathF.Max( 0, SoldierPillCooldown - Time.Delta );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || NinjaPlayer.InputBlocked ) return;
		if ( Stats.IsDowned || Stats.IsStunned ) return;

		// Pilule du soldat : touche 'J' (action "SoldierPill")
		if ( Input.Pressed( "SoldierPill" ) )
		{
			RequestUseSoldierPillRpc();
		}
	}

	/// <summary>État sauvegardé en base : « kunai,shuriken,parchemin,fumigène,pilule ».</summary>
	public string Serialize() => $"{KunaiCount},{ShurikenCount},{ExplosiveTagCount},{SmokeBombCount},{SoldierPillCount}";

	/// <summary>Recharge l'état sauvegardé. Vide ou illisible (nouveau perso) : on garde le stock de départ.</summary>
	public void Load( string saved )
	{
		var n = saved?.Split( ',' );
		if ( n is not { Length: 5 } || !n.All( x => int.TryParse( x, out var v ) && v >= 0 ) ) return;

		KunaiCount = int.Parse( n[0] );
		ShurikenCount = int.Parse( n[1] );
		ExplosiveTagCount = int.Parse( n[2] );
		SmokeBombCount = int.Parse( n[3] );
		SoldierPillCount = int.Parse( n[4] );
	}

	/// <summary>
	/// Demande au serveur de consommer une pilule militaire.
	/// </summary>
	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestUseSoldierPillRpc()
	{
		if ( Stats.IsDowned || Stats.IsStunned ) return;
		if ( SoldierPillCount <= 0 || SoldierPillCooldown > 0 ) return;

		SoldierPillCount--;
		SoldierPillCooldown = MaxSoldierPillCooldown;

		// Régénère 45 PV et 45 Chakra
		Stats.Heal( 45f );
		Stats.RestoreChakra( 45f );

		NotifyPillConsumedRpc();
		Log.Info( $"[Inventaire] {Player.CharacterName} consomme une pilule du soldat (reste: {SoldierPillCount})" );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void NotifyPillConsumedRpc()
	{
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/ui/coin.sound" )
			?? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_flesh.sound" );
		if ( snd is not null )
			Sound.Play( snd, WorldPosition );
	}

	/// <summary>
	/// Vérifie si le joueur possède au moins <paramref name="count"/> exemplaire(s) d'un objet.
	/// </summary>
	public bool HasItem( string itemId, int count = 1 )
	{
		return itemId?.ToLower() switch
		{
			"kunai" => KunaiCount >= count,
			"shuriken" => ShurikenCount >= count,
			"explosive_tag" => ExplosiveTagCount >= count,
			"smoke_bomb" => SmokeBombCount >= count,
			"soldier_pill" => SoldierPillCount >= count,
			_ => false
		};
	}

	/// <summary>
	/// Consomme un objet sur l'hôte. Retourne vrai si l'objet était disponible.
	/// </summary>
	public bool ConsumeItem( string itemId, int count = 1 )
	{
		if ( !Networking.IsHost ) return false;
		if ( !HasItem( itemId, count ) ) return false;

		switch ( itemId?.ToLower() )
		{
			case "kunai":
				KunaiCount -= count;
				break;
			case "shuriken":
				ShurikenCount -= count;
				break;
			case "explosive_tag":
				ExplosiveTagCount -= count;
				break;
			case "smoke_bomb":
				SmokeBombCount -= count;
				break;
			case "soldier_pill":
				SoldierPillCount -= count;
				break;
			default:
				return false;
		}

		return true;
	}

	/// <summary>
	/// Ajoute des objets dans l'inventaire du joueur (achats, quêtes, butins).
	/// </summary>
	public void AddItem( string itemId, int count = 1 )
	{
		if ( !Networking.IsHost || count <= 0 ) return;

		switch ( itemId?.ToLower() )
		{
			case "kunai":
				KunaiCount += count;
				break;
			case "shuriken":
				ShurikenCount += count;
				break;
			case "explosive_tag":
				ExplosiveTagCount += count;
				break;
			case "smoke_bomb":
				SmokeBombCount += count;
				break;
			case "soldier_pill":
				SoldierPillCount += count;
				break;
		}
	}
}
