using NarutoRp.Player;

namespace NarutoRp.World;

/// <summary>
/// Porte fermée d'une salle souterraine. Le client demande (touche Use), l'hôte vérifie et ouvre 4 s.
/// Le battant (enfant « Battant », collider + modèle) est actif tant que IsOpen est faux, chez tous les clients :
/// l'hôte ne peut pas déplacer un joueur, c'est donc le collider réel qui bloque.
/// </summary>
public sealed class LockedDoor : Component
{
	[Property] public DoorRule Rule { get; set; } = DoorRule.Kage;
	[Property] public string Village { get; set; }
	[Property] public string DomainId { get; set; }

	/// <summary>Sens unique : ouvrable seulement depuis le côté vers lequel pointe l'avant de la porte.</summary>
	[Property] public bool OneWay { get; set; }

	[Sync( SyncFlags.FromHost )] public bool IsOpen { get; private set; }

	/// <summary>Clan propriétaire du domaine, rempli par l'hôte (DomainRegistry, tâche 5).</summary>
	[Sync( SyncFlags.FromHost )] public string OwnerClan { get; set; }

	public const float OpenSeconds = 4f;
	public const float UseRange = 150f;
	public const float MaxRequestDistance = 200f;

	TimeUntil _close;

	GameObject Leaf => GameObject.Children.FirstOrDefault( c => c.Name == "Battant" );

	protected override void OnUpdate()
	{
		if ( Leaf is { } leaf && leaf.Enabled == IsOpen ) leaf.Enabled = !IsOpen;

		if ( !IsOpen && Input.Pressed( "Use" ) && LookedAt() ) RequestOpen();
	}

	/// <summary>Le joueur local regarde-t-il cette porte (trace depuis la caméra, ≤ UseRange) ?</summary>
	bool LookedAt()
	{
		var me = Scene.GetAllComponents<NinjaPlayer>().FirstOrDefault( n => !n.IsProxy );
		var ctrl = me?.GetComponent<PlayerController>();
		if ( ctrl is null ) return false;
		var eye = ctrl.EyePosition;
		var tr = Scene.Trace.Ray( eye, eye + ctrl.EyeAngles.Forward * UseRange ).IgnoreGameObjectHierarchy( me.GameObject ).Run();
		return tr.Hit && tr.GameObject is { } go && ( go == GameObject || go.IsDescendant( GameObject ) );
	}

	[Rpc.Host]
	public void RequestOpen()
	{
		// Jamais confiance aux arguments : le joueur est retrouvé par la connexion de l'appelant.
		var caller = Rpc.Caller;
		if ( caller is null || IsOpen ) return;
		var steamId = caller.SteamId.ValueUnsigned;
		var p = Scene.GetAllComponents<NinjaPlayer>().FirstOrDefault( n => n.SteamId == steamId );
		if ( !p.IsValid() ) return;

		// Un ninja à terre, étourdi ou en pleine action ne manipule pas une porte.
		var stats = p.GetComponent<NinjaStats>();
		if ( stats is null || stats.IsDowned || stats.IsStunned ) return;

		var offset = p.WorldPosition - WorldPosition;
		if ( offset.Length > MaxRequestDistance ) return;
		if ( OneWay && Vector3.Dot( offset, WorldRotation.Forward ) <= 0f ) return;

		if ( !DoorRules.CanOpen( Rule, Village, OwnerClan, p.Village, p.Grade, p.Clan, p.ClanValidated, p.StaffRank ) )
		{
			Log.Info( $"[Porte] {p.CharacterName} refusé sur {GameObject.Name}" );
			return;
		}

		IsOpen = true;
		_close = OpenSeconds;
	}

	protected override void OnFixedUpdate()
	{
		if ( Networking.IsHost && IsOpen && _close ) IsOpen = false;
	}
}
