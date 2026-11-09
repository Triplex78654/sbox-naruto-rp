using NarutoRp.Core;

namespace NarutoRp.Player;

/// <summary>
/// Component racine du prefab joueur : identité du perso chargé depuis l'API.
/// Tout est écrit par l'hôte uniquement.
/// </summary>
public sealed class NinjaPlayer : Component
{
	/// <summary>Clé du joueur en base (stable), jamais Connection.Id.</summary>
	[Sync( SyncFlags.FromHost )] public ulong SteamId { get; set; }

	/// <summary>Id du perso en base ; 0 = aucun perso chargé (rien n'est sauvegardé).</summary>
	[Sync( SyncFlags.FromHost )] public long CharacterId { get; set; }

	/// <summary>Nom RP « Prénom Nom ».</summary>
	[Sync( SyncFlags.FromHost )] public string CharacterName { get; set; }

	[Sync( SyncFlags.FromHost )] public string Village { get; set; }
	[Sync( SyncFlags.FromHost )] public string Grade { get; set; }

	/// <summary>null, "moderator", "staff" ou "admin".</summary>
	[Sync( SyncFlags.FromHost )] public string StaffRank { get; set; }

	/// <summary>Médic-nin : peut relever les ninjas à terre. Validé par le staff, chargé depuis la base.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsMedic { get; set; }

	[Sync( SyncFlags.FromHost )] public long Ryo { get; set; }

	/// <summary>Le joueur de ce client, ou null.</summary>
	public static NinjaPlayer Local
	{
		get
		{
			if ( !_local.IsValid() || !_local.Network.IsOwner )
				_local = Game.ActiveScene?.GetAllComponents<NinjaPlayer>().FirstOrDefault( x => x.Network.IsOwner );
			return _local;
		}
	}

	static NinjaPlayer _local;

	protected override void OnDestroy()
	{
		// Déconnexion (objet détruit car orphelin) ou fin de partie : dernière sauvegarde.
		if ( Networking.IsHost )
			GameManager.Current?.SaveCharacter( this );
	}
}
