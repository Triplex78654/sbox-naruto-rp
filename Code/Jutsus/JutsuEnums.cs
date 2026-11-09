namespace NarutoRp.Jutsus;

public enum ChakraNature { None, Katon, Futon, Raiton, Doton, Suiton }

public enum JutsuPath { Ninjutsu, Taijutsu, Kenjutsu, Genjutsu, Medical }

public enum JutsuRank { E, D, C, B, A, S }

/// <summary>Comment le jutsu atteint sa cible.</summary>
public enum JutsuDeliveryType
{
	/// <summary>Corps à corps devant le lanceur.</summary>
	Contact,
	/// <summary>Projectile qui avance jusqu'à toucher ou atteindre sa portée.</summary>
	Projectile,
	/// <summary>Tous les ninjas autour du lanceur.</summary>
	Area,
	/// <summary>Le lanceur lui-même (soin, buff).</summary>
	Self,
	/// <summary>Déplacement du lanceur vers l'avant sur Range (Shunshin).</summary>
	Movement,
}

public enum JutsuEffectType { Damage, Heal, Stun, Slow }
