namespace NarutoRp.Network.Api;

/// <summary>
/// Adresse et clé de l'API, lues dans le fichier api_config.json du dossier de données
/// de la machine hôte (FileSystem.Data). Ce fichier n'existe que sur le serveur : jamais dans Git
/// ni dans le jeu publié. Sans lui, on utilise l'API en mémoire.
/// </summary>
public sealed class ApiConfig
{
	public const string FileName = "api_config.json";

	public string BaseUrl { get; set; }
	public string ServerKey { get; set; }

	public static ApiConfig Load()
	{
		if ( !FileSystem.Data.FileExists( FileName ) ) return null;

		var config = FileSystem.Data.ReadJson<ApiConfig>( FileName );
		if ( string.IsNullOrWhiteSpace( config?.BaseUrl ) || string.IsNullOrWhiteSpace( config.ServerKey ) )
		{
			Log.Warning( $"[Api] {FileName} incomplet (BaseUrl et ServerKey requis) : API en mémoire utilisée" );
			return null;
		}
		return config;
	}

	/// <summary>
	/// Vraie API si le fichier de config existe, sinon API en mémoire. Hôte seulement.
	/// Sur un serveur dédié sans config : null (personne ne peut jouer avec des persos de test).
	/// </summary>
	public static IApiClient CreateClient()
	{
		var config = Load();
		if ( config is null && Application.IsDedicatedServer )
		{
			Log.Error( $"[Api] serveur dédié sans {FileName} dans {FileSystem.Data.GetFullPath( "/" )} : connexions refusées" );
			return null;
		}

		if ( config is null )
		{
			Log.Info( "[Api] pas de config : API en mémoire (données de test, rien n'est sauvegardé)" );
			return new MemoryApiClient();
		}

		Log.Info( $"[Api] API Cloudflare : {config.BaseUrl}" );
		return new WorkerApiClient( config );
	}
}
