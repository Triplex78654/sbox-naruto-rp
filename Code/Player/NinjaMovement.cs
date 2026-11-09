namespace NarutoRp.Player;

/// <summary>
/// Déplacements ninja : course ninja (double appui sur Shift), bond ninja (Espace en course ninja),
/// et téléportation courte demandée par l'hôte (Shunshin).
/// La position appartient au client : la vitesse est appliquée chez lui, l'hôte mesure et fait payer l'endurance
/// (NinjaStats), et valide le chakra du bond.
/// </summary>
public sealed class NinjaMovement : Component
{
	[RequireComponent] NinjaStats Stats { get; set; }
	[RequireComponent] PlayerController Controller { get; set; }

	/// <summary>Vitesse de course ninja = course normale × ce multiplicateur.</summary>
	[Property] public float NinjaRunMultiplier { get; set; } = 1.7f;

	/// <summary>Délai max entre deux appuis sur Shift pour lancer la course ninja.</summary>
	[Property] public float DoubleTapWindow { get; set; } = 0.3f;

	[Property, Group( "Bond" )] public float LeapChakraCost { get; set; } = 5f;
	[Property, Group( "Bond" )] public float LeapCooldown { get; set; } = 1f;
	[Property, Group( "Bond" )] public float LeapForward { get; set; } = 450f;
	[Property, Group( "Bond" )] public float LeapUp { get; set; } = 420f;

	/// <summary>Course ninja en cours (écrit par le propriétaire : purement visuel / HUD, l'hôte mesure la vitesse réelle).</summary>
	[Sync] public bool IsNinjaRunning { get; private set; }

	TimeSince _lastRunPress = 10f;
	TimeUntil _leapCooldown;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( NinjaPlayer.InputBlocked || Stats.IsDowned || Stats.IsStunned || Stats.IsExhausted )
		{
			IsNinjaRunning = false;
			return;
		}

		// Double appui sur Shift : course ninja tant que Shift reste enfoncé.
		if ( Input.Pressed( "Run" ) )
		{
			if ( _lastRunPress < DoubleTapWindow ) IsNinjaRunning = true;
			_lastRunPress = 0;
		}
		if ( !Input.Down( "Run" ) ) IsNinjaRunning = false;

		// Bond ninja : Espace pendant la course ninja, au sol.
		if ( IsNinjaRunning && Input.Pressed( "Jump" ) && Controller.IsOnGround )
			RequestLeap( Controller.EyeAngles.WithPitch( 0 ).Forward );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	void RequestLeap( Vector3 direction )
	{
		if ( !float.IsFinite( direction.x ) || !float.IsFinite( direction.y ) || !float.IsFinite( direction.z ) ) return;
		if ( Stats.IsDowned || Stats.IsStunned || !_leapCooldown ) return;
		if ( !Stats.TrySpendChakra( LeapChakraCost ) ) return;

		_leapCooldown = LeapCooldown;
		var flat = direction.WithZ( 0 );
		if ( flat.Length < 0.01f ) return;
		ApplyImpulse( flat.Normal * LeapForward + Vector3.Up * LeapUp );
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	void ApplyImpulse( Vector3 velocity )
	{
		var body = GetComponent<Rigidbody>();
		if ( body.IsValid() ) body.Velocity += velocity;
	}

	/// <summary>Téléportation courte (Shunshin). Appelée par l'hôte, appliquée par le propriétaire.</summary>
	[Rpc.Owner( NetFlags.HostOnly )]
	public void ApplyTeleport( Vector3 position )
	{
		WorldPosition = position;
		GameObject.Network.ClearInterpolation();
	}
}
