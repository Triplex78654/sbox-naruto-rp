using System.Threading;
using System.Threading.Tasks;

namespace NarutoRp.Network.Api;

/// <summary>
/// Accès aux données persistantes. Utilisé uniquement par l'hôte.
/// WorkerApiClient = vraie API Cloudflare ; MemoryApiClient = données en mémoire pour l'éditeur.
/// </summary>
public interface IApiClient
{
	/// <summary>Persos vivants, ban actif et rang staff d'un joueur. Lève ApiException en cas d'échec.</summary>
	Task<PlayerDto> GetPlayerAsync( ulong steamId, CancellationToken ct = default );

	/// <summary>Crée un perso, renvoie son id. Lève ApiException (UserMessage lisible) si refusé.</summary>
	Task<long> CreateCharacterAsync( ulong steamId, NewCharacter character, CancellationToken ct = default );

	/// <summary>Sauvegarde de l'état de jeu modifiable (ryô).</summary>
	Task SaveStateAsync( long characterId, long ryo, CancellationToken ct = default );

	/// <summary>Log libre (mise à terre, combat...). Les erreurs sont seulement journalisées.</summary>
	Task LogAsync( string type, ulong? actorSteamId, ulong? targetSteamId, object data, CancellationToken ct = default );
}
