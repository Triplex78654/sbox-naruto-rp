using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using NarutoRp.Core;

namespace NarutoRp.Combat;

/// <summary>
/// Générateur de camp de bandits / nukenins.
/// Maintient une population constante de bandits autour d'une zone et gère
/// le réapparition automatique (respawn) après leur défaite.
/// Exécution garantie côté hôte sous autorité serveur absolue.
/// </summary>
public sealed class BanditSpawner : Component
{
	[Property, Group( "Camp" )]
	public string CampName { get; set; } = "Camp de bandits";

	/// <summary>Type d'ennemi généré ("bandit_konoha", "bandit_suna", "bandit_kumo").</summary>
	[Property, Group( "Camp" )]
	public string EnemyType { get; set; } = "bandit_konoha";

	[Property, Group( "Camp" )]
	public string BanditDisplayName { get; set; } = "Pillard des Bois";

	[Property, Group( "Population" )]
	public int MaxBandits { get; set; } = 3;

	/// <summary>Délai de réapparition d'un bandit après sa mort (en secondes).</summary>
	[Property, Group( "Population" )]
	public float RespawnTime { get; set; } = 45f;

	/// <summary>Rayon de dispersion des apparitions autour du camp (unités).</summary>
	[Property, Group( "Population" )]
	public float SpawnRadius { get; set; } = 200f;

	[Property, Group( "Configuration Bandit" )]
	public float BanditHealth { get; set; } = 90f;

	[Property, Group( "Configuration Bandit" )]
	public float BanditDamage { get; set; } = 12f;

	/// <summary>Prefab optionnel. Si null, le bandit est instancié de manière procédurale.</summary>
	[Property, Group( "Configuration Bandit" )]
	public GameObject BanditPrefab { get; set; }

	readonly List<AiBanditController> _activeBandits = new();
	readonly List<TimeUntil> _respawnTimers = new();

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		// Apparition initiale de la garnison du camp
		for ( var i = 0; i < MaxBandits; i++ )
		{
			SpawnBandit();
		}

		Log.Info( $"[BanditSpawner] {CampName} initialisé avec {MaxBandits} bandits ({EnemyType})." );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Nettoyage des références détruites
		for ( var i = _activeBandits.Count - 1; i >= 0; i-- )
		{
			if ( !_activeBandits[i].IsValid() || _activeBandits[i].IsDefeated )
			{
				_activeBandits.RemoveAt( i );
			}
		}

		// Traitement de la file de réapparition
		for ( var i = _respawnTimers.Count - 1; i >= 0; i-- )
		{
			if ( _respawnTimers[i] <= 0 )
			{
				_respawnTimers.RemoveAt( i );

				if ( _activeBandits.Count < MaxBandits )
				{
					SpawnBandit();
				}
			}
		}
	}

	/// <summary>
	/// Fait apparaître un nouveau bandit dans le périmètre du camp.
	/// </summary>
	public AiBanditController SpawnBandit()
	{
		if ( !Networking.IsHost ) return null;

		var spawnPos = CalculateSpawnPosition();

		GameObject banditGo;

		if ( BanditPrefab.IsValid() )
		{
			banditGo = BanditPrefab.Clone( spawnPos );
		}
		else
		{
			banditGo = new GameObject( true, $"{BanditDisplayName} ({CampName})" );
			banditGo.WorldPosition = spawnPos;

			var mr = banditGo.Components.Create<SkinnedModelRenderer>();
			mr.Model = Model.Load( "models/citizen/citizen.vmdl" );

			// Sans collider, aucune trace (kunai, shuriken, jutsu) ne touche le bandit.
			var col = banditGo.Components.Create<CapsuleCollider>();
			col.Start = Vector3.Up * 12f;
			col.End = Vector3.Up * 60f;
			col.Radius = 12f;
		}

		var ctrl = banditGo.Components.GetOrCreate<AiBanditController>();
		ctrl.DisplayName = BanditDisplayName;
		ctrl.EnemyType = EnemyType;
		ctrl.MaxHealth = BanditHealth;
		ctrl.MeleeDamage = BanditDamage;
		ctrl.SpawnPosition = WorldPosition;
		ctrl.Spawner = this;

		// Objet réseau : sans ça les autres joueurs ne voient pas le bandit bouger ni mourir.
		banditGo.NetworkSpawn( new NetworkSpawnOptions { OwnerTransfer = OwnerTransfer.Fixed } );

		_activeBandits.Add( ctrl );
		return ctrl;
	}

	Vector3 CalculateSpawnPosition()
	{
		var rng = new Random();
		var angle = rng.NextDouble() * Math.PI * 2.0;
		var dist = rng.NextDouble() * SpawnRadius;

		var offset = new Vector3( (float)(Math.Cos( angle ) * dist), (float)(Math.Sin( angle ) * dist), 0f );
		var testPos = WorldPosition + offset;

		// Trace vers le sol pour caler l'apparition au relief
		var tr = Scene.Trace.Ray( testPos + Vector3.Up * 64f, testPos - Vector3.Up * 256f )
			.WithoutTags( NetConstants.TagInvisible, NetConstants.TagPlayer )
			.Run();

		return tr.Hit ? tr.HitPosition + Vector3.Up * 4f : testPos;
	}

	/// <summary>
	/// Appelé par un bandit lors de sa défaite pour enclencher le respawn.
	/// </summary>
	public void OnBanditDied( AiBanditController bandit )
	{
		if ( !Networking.IsHost ) return;

		_activeBandits.Remove( bandit );
		_respawnTimers.Add( RespawnTime );

		Log.Info( $"[BanditSpawner] {CampName} : bandit vaincu. Respawn dans {RespawnTime} s ({_activeBandits.Count}/{MaxBandits} actifs)." );
	}

	/// <summary>
	/// Pré-configure les 3 camps de bandits canoniques dans le monde s'ils ne sont pas déjà présents.
	/// 1. Camp des Forêts (Konoha)
	/// 2. Repaire du Canyon (Suna)
	/// 3. Ruines Neutres
	/// </summary>
	public static void SetupDefaultCamps( Scene scene )
	{
		if ( scene is null || !Networking.IsHost ) return;

		// Évite les doublons si des camps ont déjà été placés dans la scène
		if ( scene.GetAllComponents<BanditSpawner>().Any() ) return;

		Log.Info( "[BanditSpawner] Déploiement des 3 camps de bandits par défaut dans le monde..." );

		// 1. Camp des Forêts (Konoha) - Dans la clairière de la grande forêt du Feu
		CreateCamp(
			scene,
			"Camp des Forêts (Konoha)",
			new Vector3( -27000f, 30000f, 11f ),
			"bandit_konoha",
			"Pillard des Bois",
			3,
			45f,
			220f
		);

		// 2. Repaire du Canyon (Suna) - Sous les falaises de la Mesa du Vent
		CreateCamp(
			scene,
			"Repaire du Canyon (Suna)",
			new Vector3( 40000f, 12000f, 2.5f ),
			"bandit_suna",
			"Bandit du Désert",
			3,
			45f,
			200f
		);

		// 3. Ruines Neutres - Plain sud du Temple antique
		CreateCamp(
			scene,
			"Ruines Neutres",
			new Vector3( 0f, -7500f, 0f ),
			"bandit_kumo",
			"Nukenin des Ruines",
			4,
			40f,
			260f
		);
	}

	static void CreateCamp(
		Scene scene,
		string campName,
		Vector3 basePos,
		string enemyType,
		string displayName,
		int maxBandits,
		float respawnTime,
		float spawnRadius )
	{
		var campGo = new GameObject( true, $"Spawner_{campName}" );

		// Ajustement au niveau du sol
		var groundTrace = scene.Trace.Ray( basePos + Vector3.Up * 128f, basePos - Vector3.Up * 512f )
			.WithoutTags( NetConstants.TagInvisible, NetConstants.TagPlayer )
			.Run();

		campGo.WorldPosition = groundTrace.Hit ? groundTrace.HitPosition : basePos;

		var spawner = campGo.Components.Create<BanditSpawner>();
		spawner.CampName = campName;
		spawner.EnemyType = enemyType;
		spawner.BanditDisplayName = displayName;
		spawner.MaxBandits = maxBandits;
		spawner.RespawnTime = respawnTime;
		spawner.SpawnRadius = spawnRadius;
	}
}
