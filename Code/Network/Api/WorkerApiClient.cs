using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NarutoRp.Network.Api;

/// <summary>
/// Client de l'API Cloudflare (backend/). Le code n'a rien de secret : la clé vient de ApiConfig,
/// un fichier présent uniquement sur la machine du serveur.
/// </summary>
public sealed class WorkerApiClient : IApiClient
{
	readonly string _baseUrl;
	readonly Dictionary<string, string> _headers;

	static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 10 );

	public WorkerApiClient( ApiConfig config )
	{
		_baseUrl = config.BaseUrl.TrimEnd( '/' ) + "/v1";
		_headers = new Dictionary<string, string> { { "Authorization", $"Bearer {config.ServerKey}" } };
	}

	async Task<string> Send( string method, string path, object body, CancellationToken ct )
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource( ct );
		cts.CancelAfter( Timeout );

		HttpResponseMessage resp;
		try
		{
			resp = await Http.RequestAsync( _baseUrl + path, method, body is null ? null : Http.CreateJsonContent( body ), _headers, cts.Token );
		}
		catch ( Exception e )
		{
			throw new ApiException( $"{method} {path} : {e.Message}" );
		}

		using ( resp )
		{
			var text = await resp.Content.ReadAsStringAsync();
			if ( !resp.IsSuccessStatusCode )
			{
				var status = (int)resp.StatusCode;
				// Erreurs 4xx : le Worker explique en français ce qui ne va pas, affichable au joueur.
				string userMessage = null;
				if ( status is >= 400 and < 500 && status != 401 && status != 403 )
				{
					try { userMessage = Json.Deserialize<ErrorDto>( text )?.Error; } catch { }
				}
				throw new ApiException( $"{method} {path} : {status} {text}", status, userMessage );
			}
			return text;
		}
	}

	public async Task<PlayerDto> GetPlayerAsync( ulong steamId, CancellationToken ct = default )
	{
		var text = await Send( "GET", $"/players/{steamId}", null, ct );
		return Json.Deserialize<PlayerDto>( text ) ?? throw new ApiException( "réponse joueur vide" );
	}

	sealed class ErrorDto
	{
		[System.Text.Json.Serialization.JsonPropertyName( "error" )] public string Error { get; set; }
	}

	sealed class CreatedDto
	{
		[System.Text.Json.Serialization.JsonPropertyName( "id" )] public long Id { get; set; }
	}

	public async Task<long> CreateCharacterAsync( ulong steamId, NewCharacter c, CancellationToken ct = default )
	{
		var text = await Send( "POST", "/characters", new
		{
			steamId = steamId.ToString(),
			slot = c.Slot,
			firstName = c.FirstName,
			lastName = c.LastName,
			village = c.Village,
			nature = c.Nature,
			path = c.Path,
			clan = string.IsNullOrWhiteSpace( c.Clan ) ? null : c.Clan,
		}, ct );
		return Json.Deserialize<CreatedDto>( text )?.Id ?? throw new ApiException( "réponse de création vide" );
	}

	public Task SaveStateAsync( long characterId, long ryo, CancellationToken ct = default )
		=> Send( "PUT", $"/characters/{characterId}/state", new { ryo }, ct );

	public async Task LogAsync( string type, ulong? actorSteamId, ulong? targetSteamId, object data, CancellationToken ct = default )
	{
		try
		{
			await Send( "POST", "/logs", new
			{
				type,
				actorSteamId = actorSteamId?.ToString(),
				targetSteamId = targetSteamId?.ToString(),
				data,
			}, ct );
		}
		catch ( ApiException e )
		{
			Log.Warning( $"[Api] log {type} perdu : {e.Message}" );
		}
	}
}
