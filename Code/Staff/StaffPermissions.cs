namespace NarutoRp.Staff;

public enum StaffAction
{
	Teleport, Invisible, Spectate, Warn, Kick, Ban, PermanentBan, Grade, Jutsu, Medic, Clan, Fly,
}

/// <summary>
/// Rang minimal par action. Les actions qui touchent la base sont aussi revérifiées par le Worker
/// (backend/src/rules.ts, PERMISSIONS) : garder les deux tables cohérentes.
/// </summary>
public static class StaffPermissions
{
	static readonly string[] Ranks = { "moderator", "staff", "admin" };

	static string Required( StaffAction a ) => a switch
	{
		StaffAction.Teleport or StaffAction.Fly or StaffAction.Invisible or StaffAction.Spectate or StaffAction.Warn or StaffAction.Kick => "moderator",
		StaffAction.PermanentBan => "admin",
		_ => "staff",
	};

	public static bool Has( string rank, StaffAction action )
	{
		var have = Array.IndexOf( Ranks, rank );
		return have >= 0 && have >= Array.IndexOf( Ranks, Required( action ) );
	}

	public static bool IsStaff( string rank ) => Array.IndexOf( Ranks, rank ) >= 0;

	/// <summary>On ne sanctionne qu'un joueur de rang strictement inférieur (sans rang = -1).</summary>
	public static bool Outranks( string mine, string theirs ) => Array.IndexOf( Ranks, mine ) > Array.IndexOf( Ranks, theirs );

	/// <summary>Grades dans l'ordre de progression (comme le Worker).</summary>
	public static readonly string[] Grades = { "eleve", "genin", "chunin", "jonin", "kage" };

	/// <summary>Rang de jutsu maximum par grade (GDD) : E, D, C-B, A, S.</summary>
	public static Jutsus.JutsuRank MaxJutsuRank( string grade ) => grade switch
	{
		"genin" => Jutsus.JutsuRank.D,
		"chunin" => Jutsus.JutsuRank.B,
		"jonin" => Jutsus.JutsuRank.A,
		"kage" => Jutsus.JutsuRank.S,
		_ => Jutsus.JutsuRank.E,
	};
}
