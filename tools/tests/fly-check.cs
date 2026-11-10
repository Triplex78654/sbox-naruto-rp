// Vol staff (touche N) : vraies touches simulées par Input.OnButton (interne, par réflexion) : N, Espace, W, Shift, Ctrl, puis N.
// En Play, perso staff apparu : sbox-cli --call run_csharp '{"path":"<chemin absolu>/tools/tests/fly-check.cs"}'. Lire les valeurs renvoyées.
var onButton = typeof( Input ).GetMethod( "OnButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static );
var codeType = onButton.GetParameters()[0].ParameterType;
void Key( string k, bool down ) => onButton.Invoke( null, new object[] { System.Activator.CreateInstance( codeType ), k, down } );
async System.Threading.Tasks.Task Tap( string k ) { Key( k, true ); await System.Threading.Tasks.Task.Delay( 100 ); Key( k, false ); await System.Threading.Tasks.Task.Delay( 200 ); }
async System.Threading.Tasks.Task Hold( float s, params string[] ks ) { foreach ( var k in ks ) Key( k, true ); await System.Threading.Tasks.Task.Delay( (int)(s * 1000) ); foreach ( var k in ks ) Key( k, false ); await System.Threading.Tasks.Task.Delay( 100 ); }

var me = NarutoRp.Player.NinjaPlayer.Local;
var cmd = me.GetComponent<NarutoRp.Staff.StaffCommands>();
var pc = me.GetComponent<PlayerController>();
var log = new System.Text.StringBuilder();
await Tap( "n" );
log.AppendLine( $"N : flying={cmd.IsFlying} motion={pc.Body.MotionEnabled} colliders={pc.ColliderObject.Enabled} vitesse={cmd.FlySpeed}" );
var p0 = me.WorldPosition;
await Hold( 1f, "space" );
var p1 = me.WorldPosition;
log.AppendLine( $"Espace 1 s : dz={p1.z - p0.z:0}" );
var yaw = pc.EyeAngles.yaw;
await Hold( 1f, "w" );
var p2 = me.WorldPosition;
log.AppendLine( $"W 1 s : dxy={(p2 - p1).WithZ( 0 ).Length:0} (yaw {yaw:0}, dir {(p2 - p1).WithZ( 0 ).Normal})" );
await Hold( 1f, "w", "shift" );
var p3 = me.WorldPosition;
log.AppendLine( $"W+Shift 1 s : dxy={(p3 - p2).WithZ( 0 ).Length:0}" );
var ground = Game.ActiveScene.Trace.Ray( p3, p3 + Vector3.Down * 100000f ).IgnoreGameObjectHierarchy( me.GameObject ).Run();
await Hold( 1.5f, "ctrl", "shift" );
var p4 = me.WorldPosition;
log.AppendLine( $"Ctrl+Shift 1,5 s : z {p3.z:0} -> {p4.z:0}, sol à {ground.HitPosition.z:0} ({ground.GameObject?.Name}) traversé={p4.z < ground.HitPosition.z - 100}" );
await Hold( 1.5f, "space", "shift" );
var p5 = me.WorldPosition;
log.AppendLine( $"remonté : z={p5.z:0}" );
await Tap( "n" );
await System.Threading.Tasks.Task.Delay( 4000 );
var p6 = me.WorldPosition;
var g2 = Game.ActiveScene.Trace.Ray( p6 + Vector3.Up * 50f, p6 + Vector3.Down * 100000f ).IgnoreGameObjectHierarchy( me.GameObject ).Run();
log.AppendLine( $"N : flying={cmd.IsFlying} motion={pc.Body.MotionEnabled} colliders={pc.ColliderObject.Enabled} input={pc.UseInputControls} auSol={pc.IsOnGround} z={p6.z:0} sol={g2.HitPosition.z:0} dxy chute={(p6 - p5).WithZ( 0 ).Length:0}" );
return log.ToString();
