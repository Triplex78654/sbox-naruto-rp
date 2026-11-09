using System.Threading;
using System.Threading.Tasks;

namespace NarutoRp.Network.Api;

/// <summary>
/// Fausse API en mémoire pour l'éditeur (pas de fichier de config API).
/// Chaque joueur reçoit un perso de test avec les jutsus de test, comme s'il venait de la base.
/// Les données vivent tant que l'éditeur est ouvert.
/// </summary>
public sealed class MemoryApiClient : IApiClient
{
	static readonly Dictionary<ulong, PlayerDto> Players = new();
	static long _nextId = 1;

	public Task<PlayerDto> GetPlayerAsync( ulong steamId, CancellationToken ct = default )
	{
		lock ( Players )
		{
			if ( !Players.TryGetValue( steamId, out var p ) )
			{
				p = new PlayerDto
				{
					SteamId = steamId.ToString(),
					Characters =
					{
						new CharacterDto
						{
							Id = _nextId++, Slot = 1, FirstName = "Ninja", LastName = "Test", Village = "konoha",
							Nature = "raiton", Path = "ninjutsu", Grade = "genin", IsMedic = true, Ryo = 100,
							Jutsus = { "test_contact", "test_projectile", "test_zone" },
						},
					},
				};
				Players[steamId] = p;
			}
			return Task.FromResult( p );
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
