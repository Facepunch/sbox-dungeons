﻿
using Sandbox;

namespace Dungeons;

internal partial class Player : AnimatedEntity
{

	public PlayerController Controller
	{
		get => Components.Get<PlayerController>();
		set => Components.Add( value );
	}

	public CameraMode Camera
	{
		get => Components.Get<CameraMode>();
		set => Components.Add( value );
	}

	public PlayerAnimator Animator
	{
		get => Components.Get<PlayerAnimator>();
		set => Components.Add( value );
	}

	[Net]
	public SpotLightEntity LightRadius { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		Controller = new PlayerController();
		Camera = new PlayerCamera();
		Animator = new PlayerAnimator();
		PhysicsEnabled = true;
		EnableAllCollisions = true;

		LightRadius = new();
		LightRadius.SetParent( this );
		LightRadius.LocalPosition = Vector3.Up * 100f;
		LightRadius.LocalRotation = Rotation.LookAt( Vector3.Down );

		SetModel( "models/citizen/citizen.vmdl" );
		SetupPhysicsFromAABB( PhysicsMotionType.Keyframed, new Vector3( -16, -16, 0 ), new Vector3( 16, 16, 64 ) );
	}

	public override void Simulate( Client cl )
	{
		base.Simulate( cl );

		Controller.Simulate();
		Animator.Simulate();

		if ( IsServer )
		{
			LightRadius.LocalPosition = Vector3.Up * 150;
			LightRadius.Rotation = Rotation.FromPitch( 85 );
			LightRadius.DynamicShadows = true;
			LightRadius.Range = 250f;
			LightRadius.Color = Color.White.Darken( .9f );
			LightRadius.UseFogNoShadows();

			if ( Input.Pressed( InputButton.PrimaryAttack ) )
			{
				var start = Input.Cursor.Origin;
				var end = Input.Cursor.Origin + Input.Cursor.Direction * 5000f;
				var tr = Trace.Ray( start, end )
					.WorldOnly()
					.Run();

				if ( tr.Hit )
				{
					DebugOverlay.TraceResult( tr, 10f );

					new Monster().Position = tr.EndPosition;
				}
			}
		}
	}

	public override void FrameSimulate( Client cl )
	{
		base.FrameSimulate( cl );

		Controller.FrameSimulate();
	}

}
