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
	/// <summary>Clan original validé par le staff (GDD).</summary>
	[JsonPropertyName( "clan_validated" )] public bool ClanValidated { get; set; }
	[JsonPropertyName( "grade" )] public string Grade { get; set; }
	[JsonPropertyName( "is_medic" )] public bool IsMedic { get; set; }
	[JsonPropertyName( "ryo" )] public long Ryo { get; set; }
	[JsonPropertyName( "jutsus" )] public List<string> Jutsus { get; set; } = new();

	public string FullName => $"{FirstName} {LastName}";
}

/// <summary>Formulaire de création (étapes du GDD). Validé par l'hôte puis par le Worker.</summary>
public sealed class NewCharacter
{
	public int Slot { get; set; }
	public string FirstName { get; set; }
	public string LastName { get; set; }
	public string Village { get; set; }
	public string Nature { get; set; }
	public string Path { get; set; }
	/// <summary>Vide = sans clan.</summary>
	public string Clan { get; set; }
}

/// <summary>Erreur d'appel à l'API (réseau, statut HTTP, réponse illisible).</summary>
public sealed class ApiException : Exception
{
	/// <summary>Statut HTTP (0 = pas de réponse).</summary>
	public int Status { get; }

	/// <summary>Message du Worker affichable au joueur (ex. « emplacement déjà occupé »), sinon générique.</summary>
	public string UserMessage { get; }

	public ApiException( string message, int status = 0, string userMessage = null ) : base( message )
	{
		Status = status;
		UserMessage = userMessage ?? "Serveur de personnages indisponible, réessaie dans un instant.";
	}
}
