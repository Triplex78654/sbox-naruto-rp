using System;
using System.Linq;
using Sandbox;
using NarutoRp.Core;
using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// Sac de butin / rouleau de Ryô déposé au sol lors de la défaite d'un bandit.
/// Ramassable au contact direct ou via la touche Utiliser ('E').
/// L'autorité et la distribution des Ryô sont garanties par l'hôte.
/// </summary>
public sealed class LootDrop : Component
{
	/// <summary>Quantité de pièces de Ryô contenues dans ce sac.</summary>
	[Property, Sync( SyncFlags.FromHost )]
	public int RyoAmount { get; set; } = 50;

	/// <summary>Identifiant du type de butin pour les logs et les quêtes.</summary>
	[Property]
	public string LootType { get; set; } = "ryo_bandit";

	/// <summary>Durée de vie maximale du butin au sol avant disparition automatique (secondes).</summary>
	[Property]
	public float DespawnSeconds { get; set; } = 120f;

	/// <summary>Rayon de ramassage via interaction ('E') en unités.</summary>
	[Property]
	public float UseRadius { get; set; } = 80f;

	/// <summary>Rayon de ramassage automatique au contact direct en unités.</summary>
	[Property]
	public float ContactRadius { get; set; } = 45f;

	TimeSince _timeSinceSpawn;
	bool _pickedUp;
	Vector3 _basePosition;
	bool _basePositionSet;

	protected override void OnAwake()
	{
		_timeSinceSpawn = 0;
	}

	protected override void OnStart()
	{
		_basePosition = WorldPosition;
		_basePositionSet = true;
	}

	protected override void OnUpdate()
	{
		// Animation flottante décorative
		if ( _basePositionSet && !_pickedUp )
		{
			WorldRotation *= Rotation.FromYaw( 60f * Time.Delta );
			var bobbing = MathF.Sin( Time.Now * 3.5f ) * 2.5f;
			WorldPosition = _basePosition + Vector3.Up * bobbing;
		}

		// Interaction côté client via la touche "Use" ('E')
		if ( _pickedUp ) return;

		var localPlayer = NinjaPlayer.Local;
		if ( localPlayer.IsValid() && !localPlayer.IsProxy )
		{
			var dist = (localPlayer.WorldPosition - WorldPosition).Length;
			if ( dist <= UseRadius && Input.Pressed( "Use" ) )
			{
				RequestPickup();
			}
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || _pickedUp ) return;

		// Disparition après expiration du délai
		if ( _timeSinceSpawn >= DespawnSeconds )
		{
			GameObject.Destroy();
			return;
		}

		// Détection de contact automatique avec un joueur shinobi
		foreach ( var player in Scene.GetAllComponents<NinjaPlayer>() )
		{
			if ( !player.IsValid() || player.CharacterId <= 0 ) continue;

			var stats = player.GetComponent<NinjaStats>();
			if ( stats.IsValid() && stats.IsDowned ) continue;

			var dist = (player.WorldPosition - WorldPosition).Length;
			if ( dist <= ContactRadius )
			{
				Pickup( player );
				break;
			}
		}
	}

	/// <summary>
	/// Demande de ramassage envoyée par le client à l'hôte.
	/// </summary>
	[Rpc.Host]
	public void RequestPickup()
	{
		if ( !Networking.IsHost || _pickedUp ) return;

		var caller = Rpc.Caller;
		var player = Scene.GetAllComponents<NinjaPlayer>()
			.FirstOrDefault( p => p.Network.Owner == caller );

		if ( !player.IsValid() ) return;

		var dist = (player.WorldPosition - WorldPosition).Length;
		if ( dist > UseRadius * 1.5f ) return; // Protection anti-triche distance

		var stats = player.GetComponent<NinjaStats>();
		if ( stats.IsValid() && stats.IsDowned ) return;

		Pickup( player );
	}

	/// <summary>
	/// Effectue le ramassage sur l'hôte, crédite les Ryô et détruit l'objet.
	/// </summary>
	public void Pickup( NinjaPlayer player )
	{
		if ( !Networking.IsHost || _pickedUp || !player.IsValid() ) return;

		_pickedUp = true;

		// Crédite le joueur en Ryô
		player.Ryo += RyoAmount;
		Log.Info( $"[Loot] {player.CharacterName} a ramassé {RyoAmount} Ryô (Total: {player.Ryo})" );

		// Joue le son de pièces / ramassage
		PlayPickupSound();

		// Enregistre l'événement pour la traçabilité et le staff
		GameManager.Current?.LogEvent(
			"loot_pickup",
			player.SteamId,
			null,
			new
			{
				amount = RyoAmount,
				type = LootType,
				newBalance = player.Ryo
			}
		);

		// Détruit le conteneur de butin
		GameObject.Destroy();
	}

	void PlayPickupSound()
	{
		var snd = ResourceLibrary.Get<SoundEvent>( "sounds/ui/coin.sound" )
			?? ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/knife_world.sound" );

		if ( snd is not null )
		{
			Sound.Play( snd, WorldPosition );
		}
	}

	/// <summary>
	/// Fait apparaître un sac de butin avec un montant de Ryô aléatoire dans le monde.
	/// </summary>
	public static LootDrop Spawn( Scene scene, Vector3 position, int minRyo = 25, int maxRyo = 75 )
	{
		if ( scene is null ) return null;

		var dropObj = new GameObject( true, "LootDrop_Ryo" );

		// Trace pour caler la bourse proprement sur le sol
		var groundTrace = scene.Trace.Ray( position + Vector3.Up * 30f, position - Vector3.Up * 200f )
			.WithoutTags( NetConstants.TagInvisible, NetConstants.TagPlayer )
			.Run();

		dropObj.WorldPosition = groundTrace.Hit ? groundTrace.HitPosition + Vector3.Up * 8f : position + Vector3.Up * 8f;

		// Modèle 3D du sac ou prop shinobi
		var mr = dropObj.Components.Create<ModelRenderer>();
		// shortcut: boule dorée (le sac bag01.vmdl n'existe pas), à remplacer par un vrai modèle de bourse.
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 1f, 0.8f, 0.2f );
		dropObj.LocalScale = 0.25f;

		var loot = dropObj.Components.Create<LootDrop>();
		var rng = new Random();
		loot.RyoAmount = rng.Next( Math.Min( minRyo, maxRyo ), Math.Max( minRyo, maxRyo ) + 1 );

		// Objet réseau : sans ça les autres joueurs ne voient pas la bourse.
		dropObj.NetworkSpawn( new NetworkSpawnOptions { OwnerTransfer = OwnerTransfer.Fixed } );

		return loot;
	}
}
