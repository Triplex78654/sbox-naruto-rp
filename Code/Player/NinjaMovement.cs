namespace NarutoRp.Player;

/// <summary>
/// Déplacements ninja : course ninja (double appui sur Shift), bond ninja (Espace en course ninja),
/// et téléportation courte demandée par l'hôte (Shunshin).
/// La position appartient au client : la vitesse est appliquée chez lui, l'hôte mesure la vitesse
/// (NinjaStats, signalement anti-triche), et valide le chakra du bond.
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

	/// <summary>Dernier bond / Shunshin (hôte) : la vitesse mesurée juste après n'est pas suspecte.</summary>
	public TimeSince LastBurst { get; set; } = 10f;

	TimeSince _lastRunPress = 10f;
	TimeUntil _leapCooldown;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( NinjaPlayer.InputBlocked || Stats.IsDowned || Stats.IsStunned || GetComponent<Staff.StaffCommands>()?.IsFlying == true )
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
		if ( GetComponent<Combat.NinjaDefense>()?.IsGuarding == true ) return;
		if ( GetComponent<Staff.StaffCommands>()?.IsInvisible == true ) return;
		var flat = direction.WithZ( 0 );
		if ( flat.Length < 0.01f ) return;

		// Au sol, vérifié par l'hôte : sinon un client modifié enchaîne les bonds en l'air (vol).
		var ground = Scene.Trace.Ray( WorldPosition + Vector3.Up * 4f, WorldPosition - Vector3.Up * 12f )
			.IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( !ground.Hit ) return;

		if ( !Stats.TrySpendChakra( LeapChakraCost ) ) return;

		_leapCooldown = LeapCooldown;
		LastBurst = 0;
		ApplyImpulse( flat.Normal * LeapForward + Vector3.Up * LeapUp );
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	void ApplyImpulse( Vector3 velocity )
	{
		// Sans ça, le contrôleur se recolle au sol à la frame suivante et annule la vitesse verticale (vérifié).
		Controller.PreventGrounding( 0.3f );
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
