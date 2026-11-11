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
		if ( IsProxy || NinjaPlayer.InputBlocked ) return;

		for ( var i = 0; i < SlotActions.Length; i++ )
		{
			if ( Input.Pressed( SlotActions[i] ) )
				RequestCast( i, Controller.EyeAngles.Forward );
		}

		if ( Input.Pressed( "Attack1" ) )
		{
			// Si le joueur a une arme équipée ET dégainée, c'est l'arme qui frappe.
			// Le coup de poing à mains nues ne se déclenche que si le joueur a les mains nues (arme rangée ou absente).
			var kunai = GetComponent<NinjaKunai>();
			if ( kunai is null || !kunai.Enabled || !kunai.IsDrawn )
				RequestBasicAttack( Controller.EyeAngles.Forward );
		}
	}

	/// <summary>Id de la fiche d'attaque de base (clic gauche), connue de tous sans déblocage.</summary>
	public const string BasicAttackId = "base_coup_de_poing";

	/// <summary>
	/// Attaque du clic gauche à mains nues : coup de poing de base.
	/// </summary>
	public JutsuDefinition BasicAttack => _basicAttack ??= ResourceLibrary.GetAll<JutsuDefinition>().FirstOrDefault( j => j.Id == BasicAttackId );
	JutsuDefinition _basicAttack;
	TimeSince _lastBasicAttack = 10f;

	/// <summary>Intention du client : attaque de base (clic gauche).</summary>
	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestBasicAttack( Vector3 aim )
	{
		var jutsu = BasicAttack;
		if ( jutsu is null ) return;
		// Pas de cooldown voulu, mais une limite serveur contre les autoclics / scripts (4 coups par seconde max).
		if ( _lastBasicAttack < 0.25f ) return;
		_lastBasicAttack = 0;
		StartCast( jutsu, aim );
	}

	/// <summary>Intention du client : lancer le jutsu de la case <paramref name="slot"/>.</summary>
	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestCast( int slot, Vector3 aim )
	{
		// Ne jamais faire confiance aux arguments.
		if ( slot < 0 || slot >= Slots.Count ) return;
		if ( !float.IsFinite( aim.x ) || !float.IsFinite( aim.y ) || !float.IsFinite( aim.z ) || aim.Length < 0.01f ) return;

		StartCast( Slots[slot], aim );
	}

	/// <summary>Vérifie puis démarre l'incantation (hôte). Commun aux cases 1-9 et au clic gauche.</summary>
	void StartCast( JutsuDefinition jutsu, Vector3 aim )
	{
		if ( jutsu is null ) return;
		if ( !float.IsFinite( aim.x ) || !float.IsFinite( aim.y ) || !float.IsFinite( aim.z ) || aim.Length < 0.01f ) return;
		if ( !CanCast( jutsu ) ) return;
		// Shunshin visé à la verticale : refusé avant de payer.
		if ( jutsu.Delivery == JutsuDeliveryType.Movement && aim.WithZ( 0 ).Length < 0.01f ) return;

		Casting = jutsu;
		BroadcastCastFx( jutsu, Controller.EyePosition - Vector3.Up * 20f );
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
		if ( GetComponent<Staff.StaffCommands>()?.IsInvisible == true ) return;

		// Shunshin sans sol où atterrir : annulé sans rien payer.
		Vector3 shunshinDest = default;
		if ( jutsu.Delivery == JutsuDeliveryType.Movement && !TryGetShunshinDestination( jutsu, out shunshinDest ) ) return;

		if ( !Stats.TrySpendChakra( jutsu.ChakraCost ) ) return;

		var cooldown = MathF.Max( 0f, jutsu.CooldownSeconds );
		_cooldowns[jutsu] = cooldown;
		NotifyCooldown( jutsu, cooldown );
		Log.Info( $"[Combat] {GameObject.Name} lance {jutsu.DisplayName}" );

		var eye = Controller.EyePosition;

		switch ( jutsu.Delivery )
		{
			case JutsuDeliveryType.Self:
				BroadcastImpactFx( jutsu, WorldPosition + Vector3.Up * 40f, 48f * jutsu.FxSize );
				Apply( jutsu, Stats );
				break;

			case JutsuDeliveryType.Contact:
				var target = TraceTarget( eye, eye + _castAim * jutsu.Range, jutsu.HitRadius );
				var bandit = target is null ? TraceBandit( eye, eye + _castAim * jutsu.Range, jutsu.HitRadius ) : null;
				var impactPos = target is not null ? target.WorldPosition + Vector3.Up * 40f : (bandit is not null ? bandit.WorldPosition + Vector3.Up * 40f : eye + _castAim * jutsu.Range);
				BroadcastImpactFx( jutsu, impactPos, 32f * jutsu.FxSize );
				if ( target is not null ) Apply( jutsu, target );
				else if ( bandit is not null ) ApplyBandit( jutsu, bandit );
				break;

			case JutsuDeliveryType.Area:
				BroadcastImpactFx( jutsu, WorldPosition + Vector3.Up * 20f, jutsu.Range * jutsu.FxSize );
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
				foreach ( var b in Scene.GetAllComponents<AiBanditController>() )
				{
					if ( b.IsDefeated ) continue;
					if ( b.WorldPosition.Distance( WorldPosition ) > jutsu.Range ) continue;

					var los = Scene.Trace.Ray( eye, b.WorldPosition + Vector3.Up * 40f )
						.IgnoreGameObjectHierarchy( GameObject )
						.WithoutTags( Core.NetConstants.TagInvisible )
						.Run();
					if ( los.Hit && (los.GameObject == b.GameObject || los.GameObject?.Components.GetInAncestorsOrSelf<AiBanditController>() == b) )
						ApplyBandit( jutsu, b );
				}
				break;

			case JutsuDeliveryType.Projectile:
				_projectiles.Add( new Projectile { Jutsu = jutsu, Position = eye, Direction = _castAim } );
				BroadcastProjectileFx( jutsu, eye, _castAim );
				break;

			case JutsuDeliveryType.Movement:
				BroadcastImpactFx( jutsu, WorldPosition + Vector3.Up * 40f, 40f * jutsu.FxSize );
				BroadcastImpactFx( jutsu, shunshinDest + Vector3.Up * 40f, 40f * jutsu.FxSize );
				var movement = GetComponent<NinjaMovement>();
				if ( movement is not null ) movement.LastBurst = 0;
				movement?.ApplyTeleport( shunshinDest );
				break;
		}
	}

	/// <summary>
	/// Destination du Shunshin : vers l'avant sur Range, arrêtée avant un mur (traces à hauteur du torse et de la tête),
	/// puis posée sur le sol. Faux si pas de sol sous le point d'arrivée ou si une pente raide l'enterre.
	/// </summary>
	bool TryGetShunshinDestination( JutsuDefinition jutsu, out Vector3 dest )
	{
		dest = WorldPosition;
		var flat = _castAim.WithZ( 0 );
		if ( flat.Length < 0.01f ) return false;

		var distance = jutsu.Range;
		foreach ( var height in new[] { 40f, 64f } )
		{
			var up = Vector3.Up * height;
			var wall = Scene.Trace.Ray( WorldPosition + up, WorldPosition + up + flat.Normal * distance ).Radius( 16f )
				.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( Core.NetConstants.TagInvisible ).Run();
			if ( wall.Hit ) distance = MathF.Min( distance, wall.Distance );
		}

		// Sol sous le point d'arrivée : jusqu'à 72 au-dessus (montée) et 256 en dessous (descente).
		var point = WorldPosition + flat.Normal * distance;
		var ground = Scene.Trace.Ray( point + Vector3.Up * 72f, point - Vector3.Up * 256f )
			.IgnoreGameObjectHierarchy( GameObject ).WithoutTags( Core.NetConstants.TagInvisible ).Run();
		if ( !ground.Hit || ground.StartedSolid ) return false;

		dest = ground.HitPosition;
		return true;
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
				BroadcastImpactFx( p.Jutsu, tr.HitPosition, 40f * p.Jutsu.FxSize );
				var target = tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>();
				var bandit = tr.GameObject?.Components.GetInAncestorsOrSelf<AiBanditController>();
				if ( target is not null ) Apply( p.Jutsu, target );
				else if ( bandit is not null ) ApplyBandit( p.Jutsu, bandit );
				_projectiles.RemoveAt( i );
			}
			else if ( p.Travelled >= p.Jutsu.Range )
			{
				_projectiles.RemoveAt( i );
			}
		}
	}

	// ─── Effets visuels (hôte -> tout le monde, purement décoratif) ──────────

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	void BroadcastCastFx( JutsuDefinition jutsu, Vector3 position ) => JutsuFx.Cast( jutsu, position, GameObject );

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	void BroadcastImpactFx( JutsuDefinition jutsu, Vector3 position, float radius ) => JutsuFx.Burst( jutsu, position, radius );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	void BroadcastProjectileFx( JutsuDefinition jutsu, Vector3 start, Vector3 direction ) => JutsuFx.Projectile( jutsu, start, direction, GameObject );

	NinjaStats TraceTarget( Vector3 from, Vector3 to, float radius )
	{
		var tr = Scene.Trace.Ray( from, to )
			.Radius( radius )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( Core.NetConstants.TagInvisible )
			.Run();

		return tr.Hit ? tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>() : null;
	}

	AiBanditController TraceBandit( Vector3 from, Vector3 to, float radius )
	{
		var tr = Scene.Trace.Ray( from, to )
			.Radius( radius )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( Core.NetConstants.TagInvisible )
			.Run();

		return tr.Hit ? tr.GameObject?.Components.GetInAncestorsOrSelf<AiBanditController>() : null;
	}

	void ApplyBandit( JutsuDefinition jutsu, AiBanditController bandit )
	{
		if ( bandit is null || bandit.IsDefeated ) return;

		foreach ( var e in jutsu.Effects )
		{
			if ( e.Type == JutsuEffectType.Damage )
				bandit.TakeDamage( e.Amount, GameObject );
		}
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
