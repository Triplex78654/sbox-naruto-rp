using NarutoRp.Player;

namespace NarutoRp.Combat;

/// <summary>
/// Relève d'un ninja à terre par un médic-nin : maintenir E en regardant le blessé.
/// L'hôte vérifie le rôle, la distance et l'état pendant toute la durée.
/// </summary>
public sealed class ReviveAction : Component
{
	[RequireComponent] NinjaStats Stats { get; set; }
	[RequireComponent] NinjaPlayer Ninja { get; set; }
	[RequireComponent] PlayerController Controller { get; set; }

	[Property] public float ReviveSeconds { get; set; } = 4f;
	[Property] public float ReviveRange { get; set; } = 100f;
	[Property] public float ReviveHealthFraction { get; set; } = 0.25f;

	/// <summary>Blessé en cours de soin (null si aucun), pour le HUD.</summary>
	[Sync( SyncFlags.FromHost )] public NinjaStats ReviveTarget { get; private set; }

	/// <summary>Progression 0..1, pour le HUD.</summary>
	[Sync( SyncFlags.FromHost )] public float ReviveProgress { get; private set; }

	TimeSince _reviveStart;
	bool _sentRevive;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( Input.Down( "Use" ) && !_sentRevive )
		{
			var target = LookTarget();
			if ( target is not null )
			{
				_sentRevive = true;
				RequestRevive( target );
			}
		}
		else if ( !Input.Down( "Use" ) && _sentRevive )
		{
			_sentRevive = false;
			RequestCancelRevive();
		}
	}

	NinjaStats LookTarget()
	{
		var eye = Controller.EyePosition;
		var tr = Scene.Trace.Ray( eye, eye + Controller.EyeAngles.Forward * ReviveRange )
			.Radius( 8f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
		var stats = tr.GameObject?.Components.GetInAncestorsOrSelf<NinjaStats>();
		return stats is not null && stats.IsDowned ? stats : null;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestRevive( NinjaStats target )
	{
		if ( !CanRevive( target ) ) return;

		ReviveTarget = target;
		ReviveProgress = 0f;
		_reviveStart = 0;
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	public void RequestCancelRevive() => Cancel();

	bool CanRevive( NinjaStats target )
	{
		if ( !target.IsValid() || target == Stats || !target.IsDowned ) return false;
		if ( !Ninja.IsMedic ) return false;
		if ( Stats.IsDowned || Stats.IsStunned ) return false;
		if ( GetComponent<NinjaDefense>()?.IsGuarding == true || GetComponent<JutsuCaster>()?.Casting is not null ) return false;
		return target.WorldPosition.Distance( WorldPosition ) <= ReviveRange;
	}

	/// <summary>Annule la relève (coup reçu, éloignement...). Hôte seulement.</summary>
	public void Cancel()
	{
		if ( !Networking.IsHost ) return;
		ReviveTarget = null;
		ReviveProgress = 0f;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost || ReviveTarget is null ) return;

		if ( !CanRevive( ReviveTarget ) )
		{
			Cancel();
			return;
		}

		ReviveProgress = Math.Clamp( _reviveStart / MathF.Max( 0.1f, ReviveSeconds ), 0f, 1f );
		if ( ReviveProgress < 1f ) return;

		Log.Info( $"[Combat] {GameObject.Name} relève {ReviveTarget.GameObject.Name}" );
		ReviveTarget.Revive( ReviveHealthFraction );
		Cancel();
	}
}
