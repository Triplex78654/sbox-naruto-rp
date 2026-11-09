namespace NarutoRp.Jutsus;

/// <summary>
/// Fiche d'un jutsu (fichier .jutsu édité dans l'éditeur). Ajouter un jutsu = créer une fiche, pas du code.
/// </summary>
[AssetType( Name = "Jutsu", Extension = "jutsu", Category = "Naruto RP" )]
public sealed class JutsuDefinition : GameResource
{
	/// <summary>Identifiant stable en snake_case (ex. raiton_decharge), utilisé en base.</summary>
	public string Id { get; set; }

	public string DisplayName { get; set; }

	/// <summary>None pour les voies sans nature (taijutsu, kenjutsu...).</summary>
	public ChakraNature Nature { get; set; }

	public JutsuPath Path { get; set; }
	public JutsuRank Rank { get; set; }

	public float ChakraCost { get; set; } = 10f;
	public float CooldownSeconds { get; set; } = 5f;

	/// <summary>Temps des signes, interrompu par un coup.</summary>
	public float CastSeconds { get; set; } = 0.5f;

	public JutsuDeliveryType Delivery { get; set; }

	/// <summary>Portée (unités) : distance de contact, rayon de zone ou distance max du projectile.</summary>
	public float Range { get; set; } = 80f;

	/// <summary>Projectile seulement : vitesse en unités/s.</summary>
	public float ProjectileSpeed { get; set; } = 1500f;

	/// <summary>Projectile et contact : rayon de la zone de touche.</summary>
	public float HitRadius { get; set; } = 16f;

	public List<JutsuEffect> Effects { get; set; } = new();
}

/// <summary>Un effet appliqué à chaque cible touchée.</summary>
public sealed class JutsuEffect
{
	public JutsuEffectType Type { get; set; }

	/// <summary>Dégâts, soin, ou pourcentage de ralentissement (0.5 = -50 %).</summary>
	public float Amount { get; set; }

	/// <summary>Durée pour étourdissement et ralentissement.</summary>
	public float DurationSeconds { get; set; }
}
