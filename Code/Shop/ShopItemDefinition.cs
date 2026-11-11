namespace NarutoRp.Shop;

/// <summary>
/// Définition d'un article disponible dans les boutiques ou échoppes d'outils shinobi.
/// </summary>
public sealed class ShopItemDefinition
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public string Category { get; set; } // "Armes de jet", "Consommables & Pièges", "Rations & Médical"
	public int PriceRyo { get; set; }
	public int Quantity { get; set; } = 1;
	public string Icon { get; set; } // Emoji ou icône visuelle

	public static readonly List<ShopItemDefinition> DefaultCatalog = new()
	{
		new()
		{
			Id = "kunai",
			Name = "Lot de 5 Kunais",
			Description = "Arme de jet et d'estoc en acier trempé. Indispensable pour tout shinobi en mission.",
			Category = "Armes de jet",
			PriceRyo = 50,
			Quantity = 5,
			Icon = "🗡️"
		},
		new()
		{
			Id = "shuriken",
			Name = "Lot de 10 Shurikens",
			Description = "Étoiles d'acier affûtées conçues pour le lancer rapide en éventail.",
			Category = "Armes de jet",
			PriceRyo = 60,
			Quantity = 10,
			Icon = "✴️"
		},
		new()
		{
			Id = "explosive_tag",
			Name = "Parchemin explosif (Kibaku Fuda)",
			Description = "Papier talisman imprégné de chakra explosif. Se colle sur les parois ou le sol pour piéger l'ennemi.",
			Category = "Consommables & Pièges",
			PriceRyo = 150,
			Quantity = 2,
			Icon = "📜"
		},
		new()
		{
			Id = "smoke_bomb",
			Name = "Bombe fumigène (Kemuridama)",
			Description = "Sphère libérant un nuage opaque dense. Aveugle les assaillants et brise la ligne de mire des bandits.",
			Category = "Consommables & Pièges",
			PriceRyo = 100,
			Quantity = 2,
			Icon = "💨"
		},
		new()
		{
			Id = "soldier_pill",
			Name = "Pilule militaire (Hyôryôgan)",
			Description = "Pilule énergétique hautement concentrée. Restaure 45 PV et 45 points de Chakra en quelques secondes.",
			Category = "Rations & Médical",
			PriceRyo = 200,
			Quantity = 1,
			Icon = "💊"
		}
	};
}
