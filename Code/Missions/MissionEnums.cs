namespace NarutoRp.Missions;

/// <summary>
/// Rangs officiels des missions shinobi (univers Naruto).
/// </summary>
public enum MissionRank
{
	D,
	C,
	B,
	A,
	S
}

/// <summary>
/// Typologie des objectifs gérés par le système de missions.
/// </summary>
public enum MissionObjectiveType
{
	/// <summary>Rejoindre un point de passage ou sécuriser une zone.</summary>
	PatrolPoint,

	/// <summary>Transporter ou remettre un objet / colis à destination.</summary>
	DeliverItem,

	/// <summary>Vaincre un nombre requis d'ennemis ou belligérants ciblés.</summary>
	DefeatEnemies,

	/// <summary>Interagir avec un accessoire, meuble, autel ou mécanisme.</summary>
	InteractProp
}
