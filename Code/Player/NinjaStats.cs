using NarutoRp.Combat;
using NarutoRp.Core;

namespace NarutoRp.Player;

/// <summary>
/// Jauges du ninja : vie, chakra, endurance, et états (à terre, étourdi, ralenti).
/// L'hôte est le seul à les modifier ; les clients les reçoivent par [Sync( FromHost )].
/// </summary>
public sealed class NinjaStats : Component
{
	[RequireComponent] PlayerController Controller { get; set; }

	[Property, Group( "Vie" )] public float MaxHealth { get; set; } = 100f;
	[Property, Group( "Chakra" )] public float MaxChakra { get; set; } = 100f;
	[Property, Group( "Endurance" )] public float MaxStamina { get; set; } = 100f;

	/// <summary>Chakra par seconde en bougeant.</summary>
	[Property, Group( "Chakra" )] public float ChakraRegen { get; set; } = 1f;

	/// <summary>Chakra par seconde à l'arrêt (concentration).</summary>
	[Property, Group( "Chakra" )] public float ChakraRegenFocused { get; set; } = 5f;

	[Property, Group( "Endurance" )] public float StaminaRegen { get; set; } = 15f;
	[Property, Group( "Endurance" )] public float StaminaRunCost { get; set; } = 10f;

	/// <summary>Part d'endurance à retrouver avant de pouvoir recourir (évite les saccades).</summary>
	[Property, Group( "Endurance" )] public float StaminaRecoverFraction { get; set; } = 0.2f;

	[Sync( SyncFlags.FromHost )] public float Health { get; private set; }
	[Sync( SyncFlags.FromHost )] public float Chakra { get; private set; }
	[Sync( SyncFlags.FromHost )] public float Stamina { get; private set; }

	/// <summary>À terre : à 0 PV, jamais mort automatiquement.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsDowned { get; private set; }

	/// <summary>Plus assez d'endurance pour courir.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsExhausted { get; private set; }

	[Sync( SyncFlags.FromHost )] public bool IsStunned { get; private set; }

	/// <summary>Multiplicateur de vitesse (1 = normal).</summary>
	[Sync( SyncFlags.FromHost )] public float SpeedMultiplier { get; private set; } = 1f;

	TimeSince _lastStaminaUse;
	TimeUntil _stunEnd;
	TimeUntil _slowEnd;
	Vector3 _lastPos;
	TimeSince _speedWindow;
	float _speed;

	float _walkSpeed;
	float _runSpeed;
	bool? _appliedLocked;

	protected override void OnAwake()
	{
		_walkSpeed = Controller.WalkSpeed;
		_runSpeed = Controller.RunSpeed;

		// Dès la création, pour qu'un coup reçu avant OnStart ne fasse pas tomber à terre.
		if ( !Networking.IsHost ) return;
		Health = MaxHealth;
		Chakra = MaxChakra;
		Stamina = MaxStamina;
		_lastPos = WorldPosition;
	}

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsHost )
			TickHost();

		if ( !IsProxy )
			TickOwner();
	}

	void TickHost()
	{
		var dt = Time.Delta;

		// Vitesse mesurée sur la position (synchronisée), pas sur Controller.Velocity
		// qui n'est pas garantie pour un joueur distant. Fenêtre de 0,25 s pour lisser les ticks irréguliers.
		if ( _speedWindow >= 0.25f )
		{
			_speed = (WorldPosition - _lastPos).WithZ( 0 ).Length / _speedWindow;
			_lastPos = WorldPosition;
			_speedWindow = 0;
		}
		var speed = _speed;

		var running = speed > _walkSpeed * 1.2f;
		if ( running )
		{
			Stamina = MathF.Max( 0f, Stamina - StaminaRunCost * dt );
			_lastStaminaUse = 0;
		}
		else if ( _lastStaminaUse > 1f )
		{
			Stamina = MathF.Min( MaxStamina, Stamina + StaminaRegen * dt );
		}

		if ( Stamina <= 0f ) IsExhausted = true;
		else if ( Stamina >= MaxStamina * StaminaRecoverFraction ) IsExhausted = false;

		if ( !IsDowned )
		{
			var regen = speed < 1f ? ChakraRegenFocused : ChakraRegen;
			Chakra = MathF.Min( MaxChakra, Chakra + regen * dt );
		}

		if ( IsStunned && _stunEnd ) IsStunned = false;
		if ( SpeedMultiplier < 1f && _slowEnd ) SpeedMultiplier = 1f;
	}

	void TickOwner()
	{
		// shortcut: vitesse, blocage à terre et étourdissement sont appliqués par le client : le moteur laisse
		// la position à l'owner (vérifié : l'hôte ne peut pas la réécrire). Un tricheur peut bouger à terre,
		// mais ne peut ni lancer de jutsu ni éviter les dégâts (validés par l'hôte). Upgrade : détection côté hôte + log staff.
		Controller.WalkSpeed = _walkSpeed * SpeedMultiplier;
		Controller.RunSpeed = (IsExhausted ? _walkSpeed : _runSpeed) * SpeedMultiplier;

		// N'écrit UseInputControls qu'au changement, pour ne pas écraser d'autres systèmes (menus...).
		var locked = IsDowned || IsStunned;
		if ( _appliedLocked != locked )
		{
			Controller.UseInputControls = !locked;
			_appliedLocked = locked;
		}
	}

	/// <summary>Inflige des dégâts. Hôte seulement. Interrompt l'incantation.</summary>
	public void TakeDamage( float amount, GameObject attacker = null )
	{
		if ( !Networking.IsHost || IsDowned ) return;
		if ( !float.IsFinite( amount ) || amount <= 0f ) return;

		var defense = GetComponent<NinjaDefense>();
		if ( defense is not null ) amount = defense.ApplyGuard( amount );
		if ( amount <= 0f ) return;

		Health = MathF.Max( 0f, Health - amount );
		GetComponent<JutsuCaster>()?.Interrupt();
		GetComponent<ReviveAction>()?.Cancel();

		if ( Health <= 0f )
		{
			SetDowned( true );
			Log.Info( $"[Player] {GameObject.Name} mis à terre par {attacker?.Name ?? "inconnu"}" );
			var by = attacker?.Components.GetInAncestorsOrSelf<NinjaPlayer>();
			GameManager.Current?.LogEvent( "downed", by?.SteamId, GetComponent<NinjaPlayer>()?.SteamId,
				new { victim = GetComponent<NinjaPlayer>()?.CharacterName, attacker = by?.CharacterName } );
		}
	}

	/// <summary>Soigne. Hôte seulement. Ne relève pas : la relève passe par Revive.</summary>
	public void Heal( float amount )
	{
		if ( !Networking.IsHost || IsDowned ) return;
		if ( !float.IsFinite( amount ) || amount <= 0f ) return;

		Health = MathF.Min( MaxHealth, Health + amount );
	}

	/// <summary>Dépense de l'endurance si possible. Hôte seulement.</summary>
	public bool TrySpendStamina( float amount )
	{
		if ( !Networking.IsHost || IsDowned ) return false;
		if ( !float.IsFinite( amount ) || amount < 0f || Stamina < amount ) return false;

		Stamina -= amount;
		_lastStaminaUse = 0;
		return true;
	}

	/// <summary>Dépense du chakra si possible. Hôte seulement.</summary>
	public bool TrySpendChakra( float amount )
	{
		if ( !Networking.IsHost || IsDowned ) return false;
		if ( !float.IsFinite( amount ) || amount < 0f || Chakra < amount ) return false;

		Chakra -= amount;
		return true;
	}

	/// <summary>Étourdit (ne peut ni bouger ni lancer). Hôte seulement.</summary>
	public void ApplyStun( float seconds )
	{
		if ( !Networking.IsHost || IsDowned ) return;
		if ( !float.IsFinite( seconds ) || seconds <= 0f ) return;

		_stunEnd = MathF.Max( _stunEnd.Relative, seconds );
		IsStunned = true;
		GetComponent<JutsuCaster>()?.Interrupt();
	}

	/// <summary>Ralentit de <paramref name="fraction"/> (0.5 = -50 %). Hôte seulement.</summary>
	public void ApplySlow( float fraction, float seconds )
	{
		if ( !Networking.IsHost || IsDowned ) return;
		if ( !float.IsFinite( fraction ) || !float.IsFinite( seconds ) || seconds <= 0f ) return;

		// Un ralenti plus faible que celui en cours est ignoré (sinon il prolongerait le plus fort).
		var mult = 1f - Math.Clamp( fraction, 0f, 0.9f );
		var active = SpeedMultiplier < 1f && !_slowEnd;
		if ( active && mult > SpeedMultiplier ) return;

		_slowEnd = active && mult == SpeedMultiplier ? MathF.Max( _slowEnd.Relative, seconds ) : seconds;
		SpeedMultiplier = mult;
	}

	/// <summary>Relève un ninja à terre avec une partie de sa vie. Hôte seulement.</summary>
	public void Revive( float healthFraction = 0.25f )
	{
		if ( !Networking.IsHost || !IsDowned ) return;

		Health = MaxHealth * Math.Clamp( healthFraction, 0.01f, 1f );
		SetDowned( false );
		Log.Info( $"[Player] {GameObject.Name} est relevé" );
	}

	void SetDowned( bool downed )
	{
		IsDowned = downed;
		if ( downed )
		{
			IsStunned = false;
			SpeedMultiplier = 1f;
			GetComponent<JutsuCaster>()?.Interrupt();
			GameObject.Tags.Add( NetConstants.TagDowned );
		}
		else
		{
			GameObject.Tags.Remove( NetConstants.TagDowned );
		}
	}
}
