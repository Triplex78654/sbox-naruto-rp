using System.Text.Json.Serialization;

namespace NarutoRp.Network.Api;

// Miroir des réponses JSON du Worker (backend/src/index.ts). Aucun secret ici.

public sealed class PlayerDto
{
	[JsonPropertyName( "steamId" )] public string SteamId { get; set; }
	/// <summary>null, "moderator", "staff" ou "admin".</summary>
	[JsonPropertyName( "staffRank" )] public string StaffRank { get; set; }
	[JsonPropertyName( "activeBan" )] public BanDto ActiveBan { get; set; }
	[JsonPropertyName( "characters" )] public List<CharacterDto> Characters { get; set; } = new();
}

public sealed class BanDto
{
	[JsonPropertyName( "id" )] public long Id { get; set; }
	[JsonPropertyName( "reason" )] public string Reason { get; set; }
	/// <summary>Millisecondes depuis 1970, null = permanent.</summary>
	[JsonPropertyName( "expires_at" )] public long? ExpiresAt { get; set; }
}

public sealed class CharacterDto
{
	[JsonPropertyName( "id" )] public long Id { get; set; }
	[JsonPropertyName( "slot" )] public int Slot { get; set; }
	[JsonPropertyName( "first_name" )] public string FirstName { get; set; }
	[JsonPropertyName( "last_name" )] public string LastName { get; set; }
	[JsonPropertyName( "village" )] public string Village { get; set; }
	[JsonPropertyName( "nature" )] public string Nature { get; set; }
	[JsonPropertyName( "path" )] public string Path { get; set; }
	[JsonPropertyName( "clan" )] public string Clan { get; set; }
	[JsonPropertyName( "grade" )] public string Grade { get; set; }
	[JsonPropertyName( "is_medic" )] public bool IsMedic { get; set; }
	[JsonPropertyName( "ryo" )] public long Ryo { get; set; }
	[JsonPropertyName( "jutsus" )] public List<string> Jutsus { get; set; } = new();

	public string FullName => $"{FirstName} {LastName}";
}

/// <summary>Erreur d'appel à l'API (réseau, statut HTTP, réponse illisible).</summary>
public sealed class ApiException : Exception
{
	public ApiException( string message ) : base( message ) { }
}
