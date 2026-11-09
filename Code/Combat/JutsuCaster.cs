using NarutoRp.Jutsus;
using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// Lancement des jutsus d'un joueur. Le client envoie une intention (touche 1 à 9),
/// l'hôte vérifie tout, gère l'incantation, les cooldowns et applique les effets.
/// </summary>
public sealed class JutsuCaster : Component
{
	[RequireComponent] NinjaStats Stats { get; set; }
	[RequireComponent] PlayerController Controller { get; set; }

	/// <summary>
	/// Jutsus équipés, un par touche (1 à 9).
	/// Remplis par GameManager à partir des jutsus appris en base (ceux du prefab servent seulement de valeur par défaut).
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )] public NetList<JutsuDefinition> Slots { get; set; } = new();

	/// <summary>Jutsu en cours d'incantation (null si aucun), pour l'UI et les animations.</summary>
	[Sync( SyncFlags.FromHost )] public JutsuDefinition Casting { get; private set; }

	readonly Dictionary<JutsuDefinition, TimeUntil> _cooldowns = new();
	TimeUntil _castEnd;
	Vector3 _castAim;

	sealed class Projectile
	{
		public JutsuDefinition Jutsu;
		public Vector3 Position;
		public Vector3 Direction;
		public float Travelled;
	}

	readonly List<Projectile> _projectiles = new();

	static readonly string[] SlotActions = { "Slot1", "Slot2", "Slot3", "Slot4", "Slot5", "Slot6", "Slot7", "Slot8", "Slot9" };

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		for ( var i = 0; i < SlotActions.Length; i++ )
		{
			if ( Input.Pressed( SlotActions[i] ) )
				RequestCast( i, Controller.EyeAngles.Forward );
		}
	}

	/// <summary>Intention du client : lancer le jutsu de la case <paramref name="slot"/>.</summary>
	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestCast( int slot, Vector3 aim )
	{
		// Ne jamais faire confiance aux arguments.
		if ( slot < 0 || slot >= Slots.Count ) return;
		if ( !float.IsFinite( aim.x ) || !float.IsFinite( aim.y ) || !float.IsFinite( aim.z ) || aim.Length < 0.01f ) return;

		var jutsu = Slots[slot];
		if ( jutsu is null ) return;
		if ( !CanCast( jutsu ) ) return;

		Casting = jutsu;
		_castAim = aim.Normal;
		_castEnd = MathF.Max( 0f, jutsu.CastSeconds );
	}

	/// <summary>Vérifie, côté hôte, qu'un jutsu peut être lancé maintenant.</summary>
	public bool CanCast( JutsuDefinition jutsu )
	{
		if ( Casting is not null ) return false;
		if ( Stats.IsDowned || Stats.IsStunned ) return false;
		if ( GetComponent<NinjaDefense>()?.IsGuarding == true ) return false;
		if ( GetComponent<Staff.StaffCommands>()?.IsInvisible == true ) return false; // staff en surveillance
		if ( Stats.Chakra < jutsu.ChakraCost ) return false;
		if ( _cooldowns.TryGetValue( jutsu, out var cd ) && !cd ) return false;
		return true;
	}

	/// <summary>L'hôte prévient le propriétaire du cooldown, pour l'affichage (la vérification reste sur l'hôte).</summary>
	[Rpc.Owner( NetFlags.HostOnly )]
	void NotifyCooldown( JutsuDefinition jutsu, float seconds )
	{
		if ( jutsu is null ) return;
		_cooldowns[jutsu] = seconds;
	}

	/// <summary>Temps restant avant de pouvoir relancer (hôte, et propriétaire pour l'UI).</summary>
	public float CooldownRemaining( JutsuDefinition jutsu )
		=> _cooldowns.TryGetValue( jutsu, out var cd ) ? MathF.Max( 0f, cd.Relative ) : 0f;

	/// <summary>Interrompt l'incantation (coup reçu, mise à terre). Hôte seulement.</summary>
	public void Interrupt()
	{
		if ( !Networking.IsHost || Casting is null ) return;

		Log.Info( $"[Combat] {GameObject.Name} : {Casting.DisplayName} interrompu" );
		Casting = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		if ( Casting is not null && _castEnd )
			FinishCast();

		TickProjectiles();
	}

	void FinishCast()
	{
		var jutsu = Casting;
		Casting = null;

		// Les conditions ont pu changer pendant les signes.
		if ( Stats.IsDowned || Stats.IsStunned ) return;
		if ( GetComponent<NinjaDefense>()?.IsGuarding == true ) return;
		if ( !Stats.TrySpendChakra( jutsu.ChakraCost ) ) return;

		var cooldown = MathF.Max( 0f, jutsu.CooldownSeconds );
		_cooldowns[jutsu] = cooldown;
		NotifyCooldown( jutsu, cooldown );
		Log.Info( $"[Combat] {GameObject.Name} lance {jutsu.DisplayName}" );

		var eye = Controller.EyePosition;

		switch ( jutsu.Delivery )
		{
			case JutsuDeliveryType.Self:
				Apply( jutsu, Stats );
				break;

			case JutsuDeliveryType.Contact:
				var target = TraceTarget( eye, eye + _castAim * jutsu.Range, jutsu.HitRadius );
				if ( target is not null ) Apply( jutsu, target );
				break;

			case JutsuDeliveryType.Area:
				foreach ( var other in Scene.GetAllComponents<NinjaStats>() )
				{
					if ( other == Stats ) continue;
					if ( other.WorldPosition.Distance( WorldPosition ) > jutsu.Range ) continue;

					// Pas à travers les murs : le rayon vers le torse doit toucher la cible elle-même.
					var los = Scene.Trace.Ray( eye, other.WorldPosition + Vector3.Up * 40f )
						.IgnoreGameObjectHierarchy( GameObject )
						.WithoutTags( Core.NetConstants.TagInvisible )
						.Run();
					if ( los.Hit && los.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>() == other )
						Apply( jutsu, other );
				}
				break;

			case JutsuDeliveryType.Projectile:
				_projectiles.Add( new Projectile { Jutsu = jutsu, Position = eye, Direction = _castAim } );
				break;
		}
	}

	void TickProjectiles()
	{
		// shortcut: projectile simulé par l'hôte sans visuel (visuels avec le contenu, tâche 12),
		// et perdu si le lanceur se déconnecte pendant le vol.
		for ( var i = _projectiles.Count - 1; i >= 0; i-- )
		{
			var p = _projectiles[i];
			var step = MathF.Min( MathF.Max( 1f, p.Jutsu.ProjectileSpeed ) * Time.Delta, p.Jutsu.Range - p.Travelled );
			var next = p.Position + p.Direction * step;

			var tr = Scene.Trace.Ray( p.Position, next )
				.Radius( p.Jutsu.HitRadius )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( Core.NetConstants.TagInvisible )
				.Run();

			p.Position = next;
			p.Travelled += step;

			if ( tr.Hit )
			{
				var target = tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>();
				if ( target is not null ) Apply( p.Jutsu, target );
				_projectiles.RemoveAt( i );
			}
			else if ( p.Travelled >= p.Jutsu.Range )
			{
				_projectiles.RemoveAt( i );
			}
		}
	}

	NinjaStats TraceTarget( Vector3 from, Vector3 to, float radius )
	{
		var tr = Scene.Trace.Ray( from, to )
			.Radius( radius )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( Core.NetConstants.TagInvisible )
			.Run();

		return tr.Hit ? tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>() : null;
	}

	void Apply( JutsuDefinition jutsu, NinjaStats target )
	{
		// Un staff invisible n'est jamais touché.
		if ( target.GetComponent<Staff.StaffCommands>()?.IsInvisible == true ) return;

		// Esquive ou substitution : tout le coup hostile est évité (dégâts et états). Jamais pour un soin ou sur soi.
		var defense = target.GetComponent<NinjaDefense>();
		var hostile = target != Stats && jutsu.Effects.Any( e => e.Type != JutsuEffectType.Heal );
		if ( hostile && defense?.TryAvoidHit( GameObject ) == true ) return;

		// Choix de design : la garde encaisse les dégâts (réduits) mais ignore étourdissement et ralenti.
		var guarding = hostile && defense?.IsGuarding == true;

		foreach ( var e in jutsu.Effects )
		{
			switch ( e.Type )
			{
				case JutsuEffectType.Damage: target.TakeDamage( e.Amount, GameObject ); break;
				case JutsuEffectType.Heal: target.Heal( e.Amount ); break;
				case JutsuEffectType.Stun: if ( !guarding ) target.ApplyStun( e.DurationSeconds ); break;
				case JutsuEffectType.Slow: if ( !guarding ) target.ApplySlow( e.Amount, e.DurationSeconds ); break;
			}
		}
	}
}
