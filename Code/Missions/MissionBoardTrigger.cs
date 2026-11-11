using NarutoRp.Player;
using NarutoRp.UI;

namespace NarutoRp.Missions;

/// <summary>
/// Déclencheur placé sur un tableau d'affichage ou le bureau du responsable des missions.
/// Permet au joueur local d'ouvrir le menu MissionBoard en appuyant sur la touche Utiliser [E].
/// </summary>
public sealed class MissionBoardTrigger : Component
{
	/// <summary>Village du bureau des missions ("Konoha", "Suna", "Kumo" ou "Tous").</summary>
	[Property] public string Village { get; set; } = "Konoha";

	/// <summary>Distance maximale d'interaction en unités Source 2.</summary>
	[Property] public float InteractionDistance { get; set; } = 180f;

	/// <summary>Texte d'invite affiché lorsque le joueur regarde le tableau.</summary>
	[Property] public string PromptText { get; set; } = "Consulter les missions shinobi [E]";

	protected override void OnUpdate()
	{
		var me = NinjaPlayer.Local;
		if ( !me.IsValid() || me.IsProxy ) return;

		// Si le menu est déjà ouvert ou que les entrées sont bloquées, ne rien faire
		if ( MissionBoardState.IsOpen ) return;

		var dist = Vector3.DistanceBetween( WorldPosition, me.WorldPosition );
		if ( dist > InteractionDistance ) return;

		if ( Input.Pressed( "Use" ) && IsLookedAt( me ) )
		{
			MissionBoardState.OpenMenu( Village );
		}
	}

	/// <summary>
	/// Vérifie si le joueur local regarde en direction de ce tableau de missions.
	/// </summary>
	bool IsLookedAt( NinjaPlayer me )
	{
		var ctrl = me.GetComponent<PlayerController>();
		if ( ctrl is null ) return false;

		var eye = ctrl.EyePosition;
		var tr = Scene.Trace.Ray( eye, eye + ctrl.EyeAngles.Forward * InteractionDistance )
			.IgnoreGameObjectHierarchy( me.GameObject )
			.Run();

		// Cible directe ou enfant de ce GameObject
		if ( tr.Hit && tr.GameObject is { } go && (go == GameObject || go.IsDescendant( GameObject )) )
			return true;

		// Tolérance : si le joueur est très proche (< 120u) et regarde vers l'objet
		var toBoard = (WorldPosition - eye).Normal;
		var dot = Vector3.Dot( ctrl.EyeAngles.Forward, toBoard );
		return dot > 0.5f;
	}
}
