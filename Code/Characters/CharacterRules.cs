using System.Text.RegularExpressions;

namespace NarutoRp.Characters;

/// <summary>
/// Règles de création de perso. Copie des règles du Worker (backend/src/rules.ts) :
/// ici pour prévenir le joueur tout de suite et pour la vérification de l'hôte ; le Worker reste l'autorité.
/// </summary>
public static class CharacterRules
{
	public static readonly string[] Villages = { "konoha", "suna", "kumo" };
	public static readonly string[] Natures = { "katon", "futon", "raiton", "doton", "suiton" };
	public static readonly string[] Paths = { "ninjutsu", "taijutsu", "kenjutsu", "genjutsu", "medical" };

	public const int MaxSlots = 2;

	// À garder identique à la liste CANON de backend/src/rules.ts.
	static readonly HashSet<string> Canon = new()
	{
		"naruto", "sasuke", "sakura", "kakashi", "itachi", "madara", "obito", "minato", "kushina", "hinata",
		"neji", "gaara", "shikamaru", "choji", "ino", "rock lee", "lee", "tenten", "kiba", "shino", "jiraiya",
		"tsunade", "orochimaru", "hashirama", "tobirama", "hiruzen", "danzo", "kabuto", "nagato", "pain", "konan",
		"kisame", "deidara", "sasori", "hidan", "kakuzu", "zetsu", "killer bee", "bee", "ay", "temari", "kankuro",
		"boruto", "sarada", "mitsuki", "kawaki", "himawari", "guy", "gai", "asuma", "kurenai", "yamato", "sai",
		"iruka", "shisui", "haku", "zabuza", "kaguya", "hagoromo", "indra", "ashura", "kurama", "shukaku",
		"uzumaki", "uchiha", "senju", "hyuga", "hyuuga", "nara", "akimichi", "yamanaka", "inuzuka", "aburame",
		"sarutobi", "hatake", "otsutsuki", "namikaze", "hozuki", "yuki",
	};

	// Lettres latines seulement (accents compris) : pas de lettres d'autres alphabets qui imitent le latin.
	static readonly Regex NamePattern = new( @"^[A-Za-zÀ-ÖØ-öø-ÿ][A-Za-zÀ-ÖØ-öø-ÿ'\- ]{1,19}$" );
	static readonly Regex WordSplit = new( @"[\s'\-]+" );

	/// <summary>null si le nom est valide, sinon la raison (affichable).</summary>
	public static string CheckName( string name )
	{
		name = name?.Trim();
		if ( string.IsNullOrEmpty( name ) ) return "obligatoire";
		if ( !NamePattern.IsMatch( name ) ) return "2 à 20 lettres latines";
		var lower = name.ToLowerInvariant();
		// Mot par mot (« Naruto Uzumaki ») et collé (« N'aruto », « Na-ruto »).
		var joined = WordSplit.Replace( lower, "" );
		if ( Canon.Contains( lower ) || Canon.Contains( joined ) || WordSplit.Split( lower ).Any( Canon.Contains ) )
			return "nom de personnage ou clan canon interdit";
		return null;
	}

	/// <summary>Clan : vide (sans clan) ou nom valide.</summary>
	public static string CheckClan( string clan ) => string.IsNullOrWhiteSpace( clan ) ? null : CheckName( clan );

	/// <summary>Noms affichés.</summary>
	public static string Label( string id ) => id switch
	{
		"konoha" => "Konoha (Feu)",
		"suna" => "Suna (Vent)",
		"kumo" => "Kumo (Foudre)",
		"katon" => "Katon (feu)",
		"futon" => "Fûton (vent)",
		"raiton" => "Raiton (foudre)",
		"doton" => "Doton (terre)",
		"suiton" => "Suiton (eau)",
		"medical" => "Médical",
		"eleve" => "Élève",
		"chunin" => "Chûnin",
		"jonin" => "Jônin",
		null => "",
		_ => char.ToUpperInvariant( id[0] ) + id[1..],
	};
}
