using System.Threading;
using System.Threading.Tasks;
using NarutoRp.Characters;

namespace NarutoRp.Network.Api;

/// <summary>
/// Fausse API en mémoire pour l'éditeur (pas de fichier de config API).
/// Les joueurs commencent sans perso et passent par la création, comme sur le vrai serveur.
/// Un perso créé reçoit les jutsus de test et le statut médic, pour pouvoir tester le combat.
/// Les données vivent tant que l'éditeur est ouvert.
/// </summary>
public sealed class MemoryApiClient : IApiClient
{
	static readonly Dictionary<ulong, PlayerDto> Players = new();
	static long _nextId = 1;

	PlayerDto Get( ulong steamId )
	{
		if ( !Players.TryGetValue( steamId, out var p ) )
			Players[steamId] = p = new PlayerDto { SteamId = steamId.ToString() };
		return p;
	}

	public Task<PlayerDto> GetPlayerAsync( ulong steamId, CancellationToken ct = default )
	{
		lock ( Players ) return Task.FromResult( Get( steamId ) );
	}

	public Task<long> CreateCharacterAsync( ulong steamId, NewCharacter c, CancellationToken ct = default )
	{
		lock ( Players )
		{
			// Mêmes refus que le Worker, pour tester l'affichage des erreurs.
			var err = CharacterRules.CheckName( c.FirstName ) ?? CharacterRules.CheckName( c.LastName ) ?? CharacterRules.CheckClan( c.Clan );
			if ( err is not null ) throw new ApiException( err, 400, err );

			var p = Get( steamId );
			if ( c.Slot is < 1 or > CharacterRules.MaxSlots || p.Characters.Any( x => x.Slot == c.Slot ) )
				throw new ApiException( "slot", 409, "emplacement déjà occupé" );

			var created = new CharacterDto
			{
				Id = _nextId++, Slot = c.Slot, FirstName = c.FirstName.Trim(), LastName = c.LastName.Trim(),
				Village = c.Village, Nature = c.Nature, Path = c.Path, Clan = string.IsNullOrWhiteSpace( c.Clan ) ? null : c.Clan.Trim(),
				Grade = "eleve", IsMedic = true, Ryo = 100,
				Jutsus = { "test_contact", "test_projectile", "test_zone" },
			};
			p.Characters.Add( created );
			return Task.FromResult( created.Id );
		}
	}

	public Task SaveStateAsync( long characterId, long ryo, CancellationToken ct = default )
	{
		lock ( Players )
		{
			var c = Players.Values.SelectMany( p => p.Characters ).FirstOrDefault( c => c.Id == characterId );
			if ( c is not null ) c.Ryo = ryo;
		}
		return Task.CompletedTask;
	}

	public Task LogAsync( string type, ulong? actorSteamId, ulong? targetSteamId, object data, CancellationToken ct = default )
	{
		Log.Info( $"[Api/mémoire] log {type} {actorSteamId} -> {targetSteamId}" );
		return Task.CompletedTask;
	}
}
