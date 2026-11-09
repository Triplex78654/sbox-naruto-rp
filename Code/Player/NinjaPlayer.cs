namespace NarutoRp.Player;

/// <summary>
/// Component racine du prefab joueur : identité du perso.
/// Tout est écrit par l'hôte uniquement.
/// </summary>
public sealed class NinjaPlayer : Component
{
	/// <summary>Clé du joueur en base (stable), jamais Connection.Id.</summary>
	[Sync( SyncFlags.FromHost )] public ulong SteamId { get; set; }

	/// <summary>Nom RP du perso (vide tant que la création de perso n'existe pas).</summary>
	[Sync( SyncFlags.FromHost )] public string CharacterName { get; set; }

	/// <summary>
	/// Médic-nin : peut relever les ninjas à terre.
	/// shortcut: réglé dans le prefab pour tester ; viendra des données du perso validées par le staff (tâche 8).
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )] public bool IsMedic { get; set; }

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
}
