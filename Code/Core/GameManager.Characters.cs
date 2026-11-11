using System.Threading.Tasks;
using NarutoRp.Characters;
using NarutoRp.Network.Api;
using NarutoRp.Player;

namespace NarutoRp.Core;

/// <summary>
/// Choix et création de perso, avant l'apparition dans le monde.
/// Le client n'a encore aucun objet : on passe par des RPC statiques, l'appelant est identifié par Rpc.Caller.
/// </summary>
public sealed partial class GameManager
{
	/// <summary>Joueurs connectés qui n'ont pas encore choisi leur perso (hôte).</summary>
	readonly Dictionary<Guid, MenuSession> _menus = new();

	sealed class MenuSession
	{
		public PlayerDto Data;
		public bool Busy;
	}

	/// <summary>Résumé envoyé au client pour l'écran de choix (aucune donnée sensible).</summary>
	public sealed class CharacterSummary
	{
		public long Id { get; set; }
		public int Slot { get; set; }
		public string Name { get; set; }
		public string Village { get; set; }
		public string Grade { get; set; }
		public string Path { get; set; }
		public int Level { get; set; } = 1;
	}

	void OpenCharacterMenu( Connection channel, PlayerDto data, string message )
	{
		_menus[channel.Id] = new MenuSession { Data = data };

		var list = data.Characters.OrderBy( c => c.Slot ).Select( c => new CharacterSummary
		{
			Id = c.Id, Slot = c.Slot, Name = c.FullName, Village = c.Village, Grade = c.Grade, Path = c.Path,
			Level = c.Level > 0 ? c.Level : NinjaPlayer.CalculateLevel( c.PlayTime ),
		} ).ToList();

		using ( Rpc.FilterInclude( channel ) )
			ShowCharacterMenu( Json.Serialize( list ), message ?? "" );
	}

	// ─── Hôte → client ───────────────────────────────────────────────────────

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ShowCharacterMenu( string charactersJson, string message )
	{
		CharacterMenuState.Characters = Json.Deserialize<List<CharacterSummary>>( charactersJson ) ?? new();
		CharacterMenuState.Message = message;
		CharacterMenuState.Busy = false;
		CharacterMenuState.Open = true;
	}

	// ─── Client → hôte ───────────────────────────────────────────────────────

	/// <summary>Le joueur choisit un perso existant.</summary>
	[Rpc.Host]
	public static void RequestPlayCharacter( long characterId )
	{
		var caller = Rpc.Caller;
		var gm = Current;
		if ( gm is null || caller is null || !gm._menus.TryGetValue( caller.Id, out var session ) || session.Busy ) return;

		// Seulement un perso de SA liste, chargée par l'hôte : impossible de jouer le perso d'un autre.
		var character = session.Data.Characters.FirstOrDefault( c => c.Id == characterId );
		if ( character is null ) return;

		gm._menus.Remove( caller.Id );
		gm.Spawn( caller, session.Data, character );
	}

	/// <summary>Le joueur valide le formulaire de création.</summary>
	[Rpc.Host]
	public static void RequestCreateCharacter( int slot, string firstName, string lastName, string village, string nature, string path, string clan )
	{
		var caller = Rpc.Caller;
		var gm = Current;
		if ( gm is null || caller is null || !gm._menus.TryGetValue( caller.Id, out var session ) || session.Busy ) return;

		var form = new NewCharacter
		{
			Slot = slot, FirstName = firstName?.Trim(), LastName = lastName?.Trim(),
			Village = village, Nature = nature, Path = path, Clan = clan?.Trim(),
		};

		var error = Validate( form, session.Data );
		if ( error is not null )
		{
			gm.OpenCharacterMenu( caller, session.Data, error );
			return;
		}

		session.Busy = true; // une seule création à la fois par joueur
		_ = gm.CreateAndSpawn( caller, form );
	}

	/// <summary>Vérification de l'hôte (le Worker revérifie). null si tout va bien.</summary>
	static string Validate( NewCharacter f, PlayerDto data )
	{
		if ( f.Slot < 1 || f.Slot > CharacterRules.MaxSlots ) return "Emplacement invalide.";
		if ( data.Characters.Any( c => c.Slot == f.Slot ) ) return "Cet emplacement est déjà occupé.";
		if ( CharacterRules.CheckName( f.FirstName ) is { } e1 ) return $"Prénom : {e1}.";
		if ( CharacterRules.CheckName( f.LastName ) is { } e2 ) return $"Nom : {e2}.";
		if ( !CharacterRules.Villages.Contains( f.Village ) ) return "Choisis un village.";
		if ( !CharacterRules.Natures.Contains( f.Nature ) ) return "Choisis une nature de chakra.";
		if ( !CharacterRules.Paths.Contains( f.Path ) ) return "Choisis une voie.";
		if ( CharacterRules.CheckClan( f.Clan ) is { } e3 ) return $"Clan : {e3}.";
		return null;
	}

	async Task CreateAndSpawn( Connection channel, NewCharacter form )
	{
		var steamId = channel.SteamId.ValueUnsigned;
		PlayerDto data;
		try
		{
			var id = await Api.CreateCharacterAsync( steamId, form );
			data = await Api.GetPlayerAsync( steamId );
			data.Characters ??= new();

			if ( !this.IsValid() || !channel.IsActive ) return;

			var created = data.Characters.FirstOrDefault( c => c.Id == id );
			if ( created is null ) throw new ApiException( "perso créé introuvable" );

			Log.Info( $"[Core] {channel.DisplayName} a créé {created.FullName} ({created.Village})" );
			_menus.Remove( channel.Id );
			Spawn( channel, data, created );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Core] création de perso pour {channel.DisplayName} refusée : {e.Message}" );
			if ( !this.IsValid() || !channel.IsActive || !_menus.TryGetValue( channel.Id, out var session ) ) return;

			var message = e is ApiException api ? api.UserMessage : null;
			message = string.IsNullOrWhiteSpace( message ) ? "Erreur inattendue, réessaie." : char.ToUpperInvariant( message[0] ) + message[1..];

			// La création a pu réussir avant l'erreur : on recharge la liste pour que le perso apparaisse.
			var fresh = session.Data;
			try { fresh = await Api.GetPlayerAsync( steamId ); fresh.Characters ??= new(); } catch { }
			if ( !this.IsValid() || !channel.IsActive ) return;
			OpenCharacterMenu( channel, fresh, message );
		}
	}
}

/// <summary>État de l'écran de choix côté client (rempli par ShowCharacterMenu).</summary>
public static class CharacterMenuState
{
	public static bool Open { get; set; }
	public static bool Busy { get; set; }
	public static string Message { get; set; }
	public static List<GameManager.CharacterSummary> Characters { get; set; } = new();
}
