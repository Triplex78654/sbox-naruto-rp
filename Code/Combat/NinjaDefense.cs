using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// Défenses du ninja : garde (clic droit maintenu), esquive (X), substitution (Z).
/// Le client demande, l'hôte valide, dépense et applique. NinjaStats.TakeDamage passe par ModifyIncomingDamage.
/// </summary>
public sealed class NinjaDefense : Component
{
	[RequireComponent] NinjaStats Stats { get; set; }
	[RequireComponent] PlayerController Controller { get; set; }

	[Property, Group( "Garde" )] public float GuardDamageReduction { get; set; } = 0.6f;
	/// <summary>Endurance perdue par point de dégât bloqué.</summary>
	[Property, Group( "Garde" )] public float GuardStaminaPerDamage { get; set; } = 0.5f;

	[Property, Group( "Esquive" )] public float DodgeStaminaCost { get; set; } = 25f;
	[Property, Group( "Esquive" )] public float DodgeCooldown { get; set; } = 1f;
	/// <summary>Durée d'invulnérabilité pendant l'esquive.</summary>
	[Property, Group( "Esquive" )] public float DodgeInvulnerableSeconds { get; set; } = 0.35f;
	[Property, Group( "Esquive" )] public float DodgeImpulse { get; set; } = 600f;

	[Property, Group( "Substitution" )] public float SubstitutionChakraCost { get; set; } = 20f;
	[Property, Group( "Substitution" )] public float SubstitutionCooldown { get; set; } = 30f;
	/// <summary>Fenêtre pendant laquelle le prochain coup est remplacé par la bûche.</summary>
	[Property, Group( "Substitution" )] public float SubstitutionWindow { get; set; } = 1f;
	[Property, Group( "Substitution" )] public float SubstitutionDistance { get; set; } = 150f;

	[Sync( SyncFlags.FromHost )] public bool IsGuarding { get; private set; }

	/// <summary>Substitution armée : le prochain coup sera annulé.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsSubstitutionReady { get; private set; }

	TimeUntil _dodgeCooldown;
	TimeUntil _invulnerableEnd;
	TimeUntil _substitutionCooldown;
	TimeUntil _substitutionEnd;

	/// <summary>Pour le HUD : cooldown de substitution connu du propriétaire.</summary>
	public float SubstitutionCooldownRemaining => MathF.Max( 0f, _substitutionCooldown.Relative );

	bool _sentGuard;
	TimeSince _lastGuardRequest;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// Renvoie la demande si l'hôte a refusé ou cassé la garde alors que le bouton est toujours enfoncé.
		var wantGuard = Input.Down( "Attack2" );
		if ( wantGuard != _sentGuard || (wantGuard && !IsGuarding && _lastGuardRequest > 0.3f) )
		{
			_sentGuard = wantGuard;
			_lastGuardRequest = 0;
			RequestGuard( wantGuard );
		}

		if ( Input.Pressed( "Dodge" ) )
		{
			// Direction voulue (ZQSD) dans le repère de la caméra, sinon vers l'arrière.
			var dir = Input.AnalogMove.WithZ( 0 );
			dir = dir.Length > 0.1f ? Controller.EyeAngles.WithPitch( 0 ).ToRotation() * dir : Controller.EyeAngles.WithPitch( 0 ).Forward * -1f;
			RequestDodge( dir.Normal );
		}

		if ( Input.Pressed( "Substitution" ) )
			RequestSubstitution();
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Garde rompue si plus d'endurance ou incapacité.
		if ( IsGuarding && (Stats.IsDowned || Stats.IsStunned || Stats.Stamina <= 0f) )
			IsGuarding = false;

		if ( IsSubstitutionReady && _substitutionEnd )
			IsSubstitutionReady = false;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestGuard( bool guard )
	{
		if ( guard && (Stats.IsDowned || Stats.IsStunned || Stats.Stamina <= 0f) ) return;
		IsGuarding = guard;
		if ( guard ) GetComponent<JutsuCaster>()?.Interrupt();
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestDodge( Vector3 dir )
	{
		if ( !float.IsFinite( dir.x ) || !float.IsFinite( dir.y ) || !float.IsFinite( dir.z ) ) return;
		if ( Stats.IsDowned || Stats.IsStunned || IsGuarding ) return;
		if ( !_dodgeCooldown ) return;
		if ( !Stats.TrySpendStamina( DodgeStaminaCost ) ) return;

		_dodgeCooldown = DodgeCooldown;
		_invulnerableEnd = DodgeInvulnerableSeconds;
		GetComponent<JutsuCaster>()?.Interrupt();

		var flat = dir.WithZ( 0 );
		if ( flat.Length < 0.01f ) return;
		ApplyDash( flat.Normal * DodgeImpulse );
	}

	/// <summary>Le déplacement appartient au propriétaire : c'est lui qui applique l'élan validé par l'hôte.</summary>
	[Rpc.Owner( NetFlags.HostOnly )]
	void ApplyDash( Vector3 velocity )
	{
		var body = GetComponent<Rigidbody>();
		if ( body.IsValid() )
			body.Velocity += velocity;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestSubstitution()
	{
		if ( Stats.IsDowned || Stats.IsStunned ) return;
		if ( !_substitutionCooldown ) return;
		if ( !Stats.TrySpendChakra( SubstitutionChakraCost ) ) return;

		_substitutionCooldown = SubstitutionCooldown;
		_substitutionEnd = SubstitutionWindow;
		IsSubstitutionReady = true;
		NotifySubstitutionCooldown( SubstitutionCooldown );
		Log.Info( $"[Combat] {GameObject.Name} prépare une substitution" );
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	void NotifySubstitutionCooldown( float seconds )
	{
		_substitutionCooldown = seconds;
	}

	/// <summary>
	/// Appelé par JutsuCaster avant d'appliquer les effets (hôte) : vrai si le coup entier est évité
	/// (esquive en cours ou substitution armée), dégâts et états compris.
	/// </summary>
	public bool TryAvoidHit( GameObject attacker )
	{
		if ( !Networking.IsHost ) return false;

		if ( !_invulnerableEnd ) return true;

		if ( IsSubstitutionReady )
		{
			IsSubstitutionReady = false;
			Log.Info( $"[Combat] {GameObject.Name} esquive par substitution le coup de {attacker?.Name ?? "inconnu"}" );

			// Recul à l'opposé de l'attaquant, appliqué par le propriétaire.
			var away = attacker.IsValid() ? (WorldPosition - attacker.WorldPosition).WithZ( 0 ) : -Controller.EyeAngles.WithPitch( 0 ).Forward;
			if ( away.Length < 0.01f ) away = Vector3.Backward;
			// On s'arrête avant un mur éventuel.
			var up = Vector3.Up * 40f;
			var target = WorldPosition + away.Normal * SubstitutionDistance;
			var tr = Scene.Trace.Ray( WorldPosition + up, target + up ).Radius( 16f ).IgnoreGameObjectHierarchy( GameObject ).Run();
			Teleport( tr.Hit ? tr.EndPosition - up : target );
			return true;
		}

		return false;
	}

	/// <summary>Appelé par NinjaStats.TakeDamage (hôte) : renvoie les dégâts subis après la garde.</summary>
	public float ApplyGuard( float amount )
	{
		if ( !Networking.IsHost || !IsGuarding ) return amount;

		// La garde ne bloque que ce que l'endurance restante peut payer.
		var blocked = amount * Math.Clamp( GuardDamageReduction, 0f, 1f );
		if ( GuardStaminaPerDamage > 0f )
			blocked = MathF.Min( blocked, Stats.Stamina / GuardStaminaPerDamage );
		Stats.TrySpendStamina( blocked * GuardStaminaPerDamage );
		return amount - blocked;
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	void Teleport( Vector3 position )
	{
		WorldPosition = position;
		GameObject.Network.ClearInterpolation();
	}
}
