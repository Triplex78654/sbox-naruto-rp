namespace NarutoRp.Missions;

/// <summary>
/// Définition d'une étape / objectif unitaire au sein d'une mission shinobi.
/// </summary>
public sealed class MissionObjectiveDefinition
{
	/// <summary>Nature de l'objectif.</summary>
	public MissionObjectiveType Type { get; set; } = MissionObjectiveType.PatrolPoint;

	/// <summary>Intitulé descriptif pour le suivi de quête.</summary>
	public string Description { get; set; } = "";

	/// <summary>Nombre requis pour valider cette étape (ex: 3 ennemis, 1 livraison...).</summary>
	public int TargetCount { get; set; } = 1;

	/// <summary>Coordonnées cibles dans le monde.</summary>
	public Vector3 TargetLocation { get; set; } = Vector3.Zero;

	/// <summary>Tag de l'entité, PNJ ou objet ciblé (ex: "pillard_canyon", "remedes_box").</summary>
	public string TargetTag { get; set; } = "";

	/// <summary>Rayon de tolérance pour valider la proximité (en unités s&amp;box Source 2, 40 u = 1 m).</summary>
	public float TargetRadius { get; set; } = 250f;
}

/// <summary>
/// Fiche de données de mission shinobi (.mission).
/// </summary>
[AssetType( Name = "Mission", Extension = "mission", Category = "Naruto RP" )]
public sealed class MissionDefinition : GameResource
{
	/// <summary>Identifiant unique snake_case (ex: "mission_d_konoha_livraison").</summary>
	public string Id { get; set; } = "";

	/// <summary>Nom officiel de la mission affiché dans les registres.</summary>
	public string Title { get; set; } = "";

	/// <summary>Description détaillée du contrat.</summary>
	public string Description { get; set; } = "";

	/// <summary>Classification de difficulté / importance (D, C, B, A, S).</summary>
	public MissionRank Rank { get; set; } = MissionRank.D;

	/// <summary>Village commanditaire ("Konoha", "Suna", "Kumo" ou "Neutre").</summary>
	public string Village { get; set; } = "Neutre";

	/// <summary>Prime allouée en Ryô (両) à l'accomplissement.</summary>
	public long RewardRyo { get; set; } = 300;

	/// <summary>Grade minimum prérequis ("Élève", "Genin", "Chûnin", "Jônin").</summary>
	public string MinGrade { get; set; } = "Genin";

	/// <summary>Séquence des objectifs à valider pour terminer la mission.</summary>
	public List<MissionObjectiveDefinition> Objectives { get; set; } = new();

	/// <summary>Vérifie si le grade du ninja est suffisant pour le prérequis de la mission.</summary>
	public static bool SatisfiesGrade( string playerGrade, string minGrade )
	{
		int RankScore( string g ) => g?.ToLowerInvariant() switch
		{
			"eleve" or "élève" or "aspirant" => 0,
			"genin" => 1,
			"chunin" or "chûnin" => 2,
			"jonin" or "jônin" => 3,
			"anbu" => 4,
			"kage" => 5,
			_ => 1
		};

		return RankScore( playerGrade ) >= RankScore( minGrade );
	}

	/// <summary>Vérifie si le ninja a accès à la mission selon son village d'affiliation.</summary>
	public static bool IsVillageCompatible( string playerVillage, string missionVillage )
	{
		if ( string.IsNullOrEmpty( missionVillage ) || missionVillage.Equals( "Neutre", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return !string.IsNullOrEmpty( playerVillage ) && missionVillage.Equals( playerVillage, StringComparison.OrdinalIgnoreCase );
	}
}
