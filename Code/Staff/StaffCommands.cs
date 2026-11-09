using System.Threading.Tasks;
using NarutoRp.Combat;
using NarutoRp.Core;
using NarutoRp.Jutsus;
using NarutoRp.Network.Api;
using NarutoRp.Player;

namespace NarutoRp.Staff;

/// <summary>
/// Commandes staff, sur le prefab joueur. Le client (panel F1) demande, l'hôte vérifie le rang de CE joueur
/// (chargé depuis la base, jamais envoyé par le client), applique, logge. Le Worker revérifie les actions en base.
/// Sert aussi à tous les joueurs pour recevoir des messages (avertissement, retour d'action).
/// </summary>
public sealed class StaffCommands : Component
{
	[RequireComponent] NinjaPlayer Ninja { get; set; }

	/// <summary>Staff invisible : caché, intouchable, ne peut pas agir en combat.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsInvisible { get; private set; }

	/// <summary>Dernier message reçu (avertissement, résultat d'action), pour le HUD. Propriétaire seulement.</summary>
	public string Notice { get; private set; }
	public TimeSince NoticeAge { get; private set; }

	/// <summary>Joueur suivi en mode spectateur (côté client du staff).</summary>
	public NinjaPlayer SpectateTarget { get; set; }

	GameManager Gm => GameManager.Current;
	bool _apiBusy;
	TimeSince _lastApi = 10f;
	string Rank => Ninja.StaffRank;

	// ─── Vérifications communes (hôte) ───────────────────────────────────────

	bool Allowed( StaffAction action )
	{
		if ( StaffPermissions.Has( Rank, action ) ) return true;
		Log.Warning( $"[Staff] {Ninja.CharacterName} ({Ninja.SteamId}) a tenté {action} sans le rang ({Rank ?? "joueur"})" );
		Notify( "Rang staff insuffisant." );
		return false;
	}

	static bool ValidTarget( NinjaPlayer target ) => target.IsValid() && target.CharacterId > 0;

	/// <summary>Un staff ne modifie pas son propre perso (sauf admin), comme le Worker.</summary>
	bool NotSelf( NinjaPlayer target )
	{
		if ( target != Ninja || Rank == "admin" ) return true;
		Notify( "Tu ne peux pas modifier ton propre perso." );
		return false;
	}

	static string CleanReason( string reason ) => reason?.Trim() is { Length: >= 3 and <= 500 } r ? r : null;

	void LogStaff( string type, NinjaPlayer target, object data )
		=> Gm?.LogEvent( type, Ninja.SteamId, target?.SteamId, data );

	/// <summary>Lance une action API et prévient le staff du résultat.</summary>
	async Task RunApi( Func<IApiClient, Task> call, string success, Action onSuccess = null )
	{
		var api = Gm?.Api;
		if ( api is null ) { Notify( "API indisponible." ); return; }
		// Une action en base à la fois par staff (double clic, spam).
		if ( _apiBusy || _lastApi < 0.5f ) { Notify( "Action précédente en cours…" ); return; }
		_apiBusy = true;
		_lastApi = 0;
		try
		{
			await call( api );
		}
		catch ( Exception e )
		{
			Notify( e is ApiException a ? $"Refusé : {a.UserMessage}" : "Erreur inattendue." );
			Log.Warning( $"[Staff] action de {Ninja.CharacterName} échouée : {e.Message}" );
			return;
		}
		finally
		{
			_apiBusy = false;
		}

		// Succès en base : mise à jour en jeu (la cible a pu partir pendant l'appel).
		try { onSuccess?.Invoke(); } catch ( Exception e ) { Log.Warning( $"[Staff] mise à jour en jeu : {e.Message}" ); }
		Notify( success );
	}

	// ─── Messages vers le propriétaire ───────────────────────────────────────

	/// <summary>Envoie un message au propriétaire de ce joueur. Hôte seulement.</summary>
	public void Notify( string text )
	{
		if ( Networking.IsHost ) ReceiveNotice( text );
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	void ReceiveNotice( string text )
	{
		Notice = text;
		NoticeAge = 0;
	}

	// ─── Déplacement (la position appartient au client : l'hôte le lui demande) ───

	[Rpc.Owner( NetFlags.HostOnly )]
	void ApplyTeleport( Vector3 position )
	{
		WorldPosition = position;
		GameObject.Network.ClearInterpolation();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestTeleportTo( NinjaPlayer target )
	{
		if ( !Allowed( StaffAction.Teleport ) || !target.IsValid() ) return;
		ApplyTeleport( target.WorldPosition + target.WorldRotation.Backward * 60f );
		LogStaff( "staff_teleport", target, new { to = target.CharacterName } );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestBring( NinjaPlayer target )
	{
		if ( !Allowed( StaffAction.Teleport ) || !target.IsValid() ) return;
		target.GetComponent<StaffCommands>()?.ApplyTeleport( WorldPosition + WorldRotation.Forward * 60f );
		target.GetComponent<StaffCommands>()?.Notify( "Un membre du staff t'a téléporté." );
		LogStaff( "staff_bring", target, new { target = target.CharacterName } );
	}

	// ─── Invisibilité ────────────────────────────────────────────────────────

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestSetInvisible( bool invisible )
	{
		if ( !Allowed( StaffAction.Invisible ) ) return;
		IsInvisible = invisible;
		if ( invisible ) GameObject.Tags.Add( NetConstants.TagInvisible ); else GameObject.Tags.Remove( NetConstants.TagInvisible );
		GetComponent<JutsuCaster>()?.Interrupt();
		GetComponent<ReviveAction>()?.Cancel();
		LogStaff( "staff_invisible", null, new { invisible } );
		Notify( invisible ? "Invisible." : "Visible." );
	}

	protected override void OnUpdate()
	{
		// Chez tout le monde : un staff invisible n'est pas affiché (le propriétaire le voit en transparence).
		foreach ( var r in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			r.Enabled = !IsInvisible || !IsProxy;
			r.Tint = IsInvisible && !IsProxy ? Color.White.WithAlpha( 0.3f ) : Color.White;
		}
	}

	// ─── Spectateur (vue seulement, côté client du staff) ─────────────────────

	protected override void OnPreRender()
	{
		if ( IsProxy ) return;
		var controller = GetComponent<PlayerController>();
		var spectating = SpectateTarget.IsValid() && StaffPermissions.Has( Rank, StaffAction.Spectate );
		if ( controller is not null ) controller.UseCameraControls = !spectating;
		if ( !spectating ) { SpectateTarget = null; return; }

		var t = SpectateTarget.WorldTransform;
		Scene.Camera.WorldPosition = t.Position + Vector3.Up * 90f + t.Rotation.Backward * 160f;
		Scene.Camera.WorldRotation = Rotation.LookAt( t.Position + Vector3.Up * 60f - Scene.Camera.WorldPosition );
	}

	// ─── Sanctions ───────────────────────────────────────────────────────────

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestSanction( NinjaPlayer target, string type, string reason, int durationMinutes )
	{
		var action = type switch { "warn" => StaffAction.Warn, "kick" => StaffAction.Kick, "ban" => StaffAction.Ban, _ => (StaffAction?)null };
		if ( action is null || !target.IsValid() || !Allowed( action.Value ) ) return;
		if ( target == Ninja ) { Notify( "Tu ne peux pas te sanctionner toi-même." ); return; }
		if ( !StaffPermissions.Outranks( Rank, target.StaffRank ) ) { Notify( "Impossible de sanctionner un staff de rang égal ou supérieur." ); return; }
		if ( (reason = CleanReason( reason )) is null ) { Notify( "Motif obligatoire (3 caractères minimum)." ); return; }
		if ( type == "ban" && durationMinutes is < 1 or > 525_600 ) { Notify( "Durée de ban invalide (1 min à 1 an)." ); return; }

		var connection = target.Network.Owner;
		var name = target.CharacterName;
		_ = RunApi(
			api => api.AddSanctionAsync( target.SteamId, type, reason, type == "ban" ? durationMinutes : null, Ninja.SteamId ),
			$"{type} appliqué à {name}.",
			() =>
			{
				if ( type == "warn" ) { if ( target.IsValid() ) target.GetComponent<StaffCommands>()?.Notify( $"Avertissement du staff : {reason}" ); }
				else if ( connection is not null && !connection.IsHost )
					connection.Kick( type == "ban" ? $"Banni {durationMinutes} min : {reason}" : $"Expulsé : {reason}" );
			} );
	}

	// ─── Progression du perso ────────────────────────────────────────────────

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestSetGrade( NinjaPlayer target, string grade, string reason )
	{
		if ( !Allowed( StaffAction.Grade ) || !ValidTarget( target ) || !NotSelf( target ) ) return;
		if ( !StaffPermissions.Grades.Contains( grade ) ) { Notify( "Grade inconnu." ); return; }
		if ( (reason = CleanReason( reason )) is null ) { Notify( "Motif obligatoire (3 caractères minimum)." ); return; }

		_ = RunApi( api => api.SetGradeAsync( target.CharacterId, grade, reason, Ninja.SteamId ),
			$"{target.CharacterName} passe {grade}.",
			() => { if ( target.IsValid() ) target.Grade = grade; } );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestUnlockJutsu( NinjaPlayer target, JutsuDefinition jutsu )
	{
		if ( !Allowed( StaffAction.Jutsu ) || !ValidTarget( target ) || !NotSelf( target ) || jutsu is null ) return;

		// Le Worker n'a pas les fiches : c'est ici qu'on vérifie grade, nature et voie (GDD).
		if ( CheckJutsuFor( target, jutsu ) is { } why ) { Notify( why ); return; }

		var caster = target.GetComponent<JutsuCaster>();
		var barFull = caster is not null && !caster.Slots.Contains( jutsu ) && caster.Slots.Count >= 9;
		_ = RunApi( api => api.UnlockJutsuAsync( target.CharacterId, jutsu.Id, Ninja.SteamId ),
			barFull
				? $"{jutsu.DisplayName} débloqué pour {target.CharacterName}, mais sa barre est pleine (9) : il ne l'aura pas en jeu."
				: $"{jutsu.DisplayName} débloqué pour {target.CharacterName}.",
			() =>
			{
				if ( target.IsValid() && caster.IsValid() && !caster.Slots.Contains( jutsu ) && caster.Slots.Count < 9 ) caster.Slots.Add( jutsu );
			} );
	}

	/// <summary>null si le perso peut apprendre ce jutsu, sinon la raison.</summary>
	public static string CheckJutsuFor( NinjaPlayer target, JutsuDefinition jutsu )
	{
		if ( string.IsNullOrEmpty( jutsu.Id ) ) return "Fiche de jutsu sans identifiant.";
		if ( jutsu.Rank > StaffPermissions.MaxJutsuRank( target.Grade ) ) return $"Rang {jutsu.Rank} trop élevé pour le grade {target.Grade}.";
		if ( jutsu.Nature != ChakraNature.None )
		{
			// shortcut: seule la 1re nature est connue du jeu ; la 2e nature (chûnin) n'a pas encore de route.
			if ( !string.Equals( jutsu.Nature.ToString(), target.Nature, StringComparison.OrdinalIgnoreCase ) )
				return $"Nature {jutsu.Nature} non maîtrisée.";
		}
		else if ( jutsu.Path != JutsuPath.Ninjutsu && !string.Equals( jutsu.Path.ToString(), target.Path, StringComparison.OrdinalIgnoreCase ) )
		{
			return $"Voie {jutsu.Path} non suivie.";
		}
		return null;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestSetMedic( NinjaPlayer target, bool isMedic )
	{
		if ( !Allowed( StaffAction.Medic ) || !ValidTarget( target ) || !NotSelf( target ) ) return;
		_ = RunApi( api => api.SetMedicAsync( target.CharacterId, isMedic, Ninja.SteamId ),
			$"{target.CharacterName} : médic-nin {(isMedic ? "oui" : "non")}.",
			() => { if ( target.IsValid() ) target.IsMedic = isMedic; } );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestValidateClan( NinjaPlayer target, bool approved )
	{
		if ( !Allowed( StaffAction.Clan ) || !ValidTarget( target ) || !NotSelf( target ) ) return;
		_ = RunApi( api => api.ValidateClanAsync( target.CharacterId, approved, Ninja.SteamId ),
			$"Clan de {target.CharacterName} {(approved ? "validé" : "refusé")}.",
			() =>
			{
				if ( !target.IsValid() ) return;
				if ( approved ) target.ClanValidated = true;
				else { target.Clan = null; target.ClanValidated = false; }
			} );
	}
}
