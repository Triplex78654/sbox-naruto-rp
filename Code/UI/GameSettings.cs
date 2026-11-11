using Sandbox.Audio;

namespace NarutoRp.UI;

/// <summary>
/// Réglages du joueur propres à ce jeu, gardés sur son PC (cookies s&box).
/// La sensibilité de la souris, le champ de vision et les volumes détaillés restent dans les options
/// de s&box : le jeu peut seulement les lire (Preferences).
/// </summary>
public static class GameSettings
{
	/// <summary>Volume général du jeu, de 0 à 1.</summary>
	public static float Volume { get; private set; } = 1f;

	/// <summary>Taille du HUD, de 0,7 à 1,4.</summary>
	public static float HudScale { get; private set; } = 1f;

	public static bool HudVisible { get; private set; } = true;

	public static void Load()
	{
		SetVolume( Game.Cookies.Get( "nrp.volume", 1f ) );
		SetHudScale( Game.Cookies.Get( "nrp.hudscale", 1f ) );
		SetHudVisible( Game.Cookies.Get( "nrp.hud", true ) );
	}

	public static void SetVolume( float value )
	{
		Volume = Math.Clamp( MathF.Round( value, 1 ), 0f, 1f );
		Mixer.Master.Volume = Volume;
		Game.Cookies.Set( "nrp.volume", Volume );
	}

	public static void SetHudScale( float value )
	{
		HudScale = Math.Clamp( MathF.Round( value, 1 ), 0.7f, 1.4f );
		Game.Cookies.Set( "nrp.hudscale", HudScale );
	}

	public static void SetHudVisible( bool value )
	{
		HudVisible = value;
		Game.Cookies.Set( "nrp.hud", value );
	}
}
