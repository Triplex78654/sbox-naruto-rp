using Sandbox.Network;
using NarutoRp.Player;

namespace NarutoRp.Core;

/// <summary>
/// Crée le lobby et fait apparaître un joueur pour chaque connexion.
/// Inspiré du NetworkHelper du moteur, mais le joueur n'a pas le droit de transférer son objet.
/// Plus tard : chargement du perso depuis l'API avant le spawn (tâche 8).
/// </summary>
public sealed class GameManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }

	/// <summary>Crée un lobby au lancement si on n'est pas déjà en réseau.</summary>
	[Property] public bool StartServer { get; set; } = true;

	protected override void OnStart()
	{
		if ( StartServer && !Networking.IsActive )
			Networking.CreateLobby( new LobbyConfig() );
	}

	// Appelé sur l'hôte seulement, quand le client a fini de charger.
	void Component.INetworkListener.OnActive( Connection channel )
	{
		if ( !PlayerPrefab.IsValid() )
		{
			Log.Warning( "[Core] GameManager : aucun PlayerPrefab assigné" );
			return;
		}

		var spawn = PickSpawn();
		var player = PlayerPrefab.Clone( spawn, name: $"Ninja - {channel.DisplayName}" );
		player.Tags.Add( NetConstants.TagPlayer );

		var ninja = player.Components.Get<NinjaPlayer>();
		if ( ninja.IsValid() )
			ninja.SteamId = channel.SteamId.ValueUnsigned;

		// Le client contrôle son déplacement, mais seul l'hôte peut changer le propriétaire.
		// Le joueur est détruit s'il se déconnecte.
		player.NetworkSpawn( new NetworkSpawnOptions
		{
			Owner = channel,
			OwnerTransfer = OwnerTransfer.Fixed,
			OrphanedMode = NetworkOrphaned.Destroy,
		} );

		Log.Info( $"[Core] {channel.DisplayName} ({channel.SteamId}) est apparu" );
	}

	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		Log.Info( $"[Core] {channel.DisplayName} s'est déconnecté" );
	}

	Transform PickSpawn()
	{
		var points = Scene.GetAllComponents<SpawnPoint>().ToArray();
		if ( points.Length == 0 )
			return WorldTransform;

		return Game.Random.FromArray( points ).WorldTransform;
	}
}
