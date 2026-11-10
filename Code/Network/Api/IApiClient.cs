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

	// Actions staff : staffSteamId = staff connecté, revérifié par le Worker. Lèvent ApiException (UserMessage) si refusé.
	Task SetGradeAsync( long characterId, string grade, string reason, ulong staffSteamId, CancellationToken ct = default );
	Task UnlockJutsuAsync( long characterId, string jutsuId, ulong staffSteamId, CancellationToken ct = default );
	Task SetMedicAsync( long characterId, bool isMedic, ulong staffSteamId, CancellationToken ct = default );
	Task ValidateClanAsync( long characterId, bool approved, ulong staffSteamId, CancellationToken ct = default );
	/// <summary>Attribue un domaine de clan (clan null = retirer). Le Worker exige un clan validé du même village.</summary>
	Task SetDomainClanAsync( string domainId, string village, string clan, ulong staffSteamId, CancellationToken ct = default );
	/// <summary>Domaines connus de la base (clan null = libre).</summary>
	Task<List<DomainDto>> GetDomainsAsync( CancellationToken ct = default );
	/// <summary>type : warn, kick, ban. durationMinutes null = permanent (ban, admin seulement).</summary>
	Task AddSanctionAsync( ulong steamId, string type, string reason, int? durationMinutes, ulong staffSteamId, CancellationToken ct = default );

	/// <summary>Log libre (mise à terre, combat...). Les erreurs sont seulement journalisées.</summary>
	Task LogAsync( string type, ulong? actorSteamId, ulong? targetSteamId, object data, CancellationToken ct = default );
}
