namespace NarutoRp.World;

/// <summary>Qui peut ouvrir une porte de salle souterraine.</summary>
public enum DoorRule { Kage, Council, SpecialUnit, Police, Clan, Staff }

/// <summary>Règles d'ouverture des portes, pures (testées par tools/tests/door-rules.cs).</summary>
public static class DoorRules
{
	public static bool CanOpen( DoorRule rule, string doorVillage, string domainClan, string village, string grade,
		string clan, bool clanValidated, string staffRank )
	{
		// Le staff ouvre tout.
		if ( !string.IsNullOrEmpty( staffRank ) ) return true;

		var sameVillage = !string.IsNullOrEmpty( doorVillage ) && village == doorVillage;
		return rule switch
		{
			// shortcut: Conseil, unité spéciale et police = Kage tant que ces rôles n'existent pas.
			DoorRule.Kage or DoorRule.Council or DoorRule.SpecialUnit or DoorRule.Police => grade == "kage" && sameVillage,
			DoorRule.Clan => !string.IsNullOrEmpty( domainClan ) && clan == domainClan && clanValidated && sameVillage,
			_ => false, // Staff : staff seul (déjà traité)
		};
	}
}
