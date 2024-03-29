﻿
using Dungeons.Attributes;
using Dungeons.Data;
using Dungeons.Items;
using Dungeons.Stash;
using Sandbox;
using System.Collections.Generic;
using System.Linq;

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

	public NavigationAgent Agent
	{
		get => Components.Get<NavigationAgent>();
		set => Components.Add( value );
	}

	[Net]
	public StatSystem Stats { get; set; }

	[Net]
	public StashEntity Stash { get; set; }

	[Net]
	public StashEntity Stash2 { get; set; }

	[Net]
	public StashEntity StashEquipment { get; set; }

	[Net]
	public SpotLightEntity LightRadius { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		Controller = new PlayerController();
		Camera = new PlayerCamera();
		Animator = new PlayerAnimator();
		Agent = new NavigationAgent();
		PhysicsEnabled = true;
		EnableAllCollisions = true;

		LightRadius = new();
		LightRadius.SetParent( this );
		LightRadius.LocalPosition = Vector3.Up * 100f;
		LightRadius.Rotation = Rotation.FromPitch( 85 );
		LightRadius.DynamicShadows = true;
		LightRadius.Range = 250f;
		LightRadius.Color = Color.White.Darken( .9f );

		Stash = new();
		Stash.Parent = this;
		Stash.Owner = this;
		Stash.LocalPosition = 0;
		Stash.SlotCount = 40;
		Stash.AddConstraint( new OccupiedConstraint() );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );
		Stash.AddWithNextAvailableSlot( new Stashable( ItemGenerator.Random( Rand.Int( 1, 16 ) ) ) );

		StashEquipment = new();
		StashEquipment.Parent = this;
		StashEquipment.Owner = this;
		StashEquipment.LocalPosition = 0;
		StashEquipment.SlotCount = 8;
		StashEquipment.AddConstraint( new ItemTypeConstraint() );
		StashEquipment.AddConstraint( new OccupiedConstraint() );

		Stats = new();
		Stats.Add( StatTypes.Life, StatModifiers.Flat, 55 );
		Stats.Add( StatTypes.LightRadius, StatModifiers.Flat, 45 );

		SetModel( "models/citizen/citizen.vmdl" );
		SetupPhysicsFromAABB( PhysicsMotionType.Keyframed, new Vector3( -16, -16, 0 ), new Vector3( 16, 16, 64 ) );
	}

	public override void ClientSpawn()
	{
		base.ClientSpawn();

		Stash.AddConstraint( new OccupiedConstraint() );
		StashEquipment.AddConstraint( new ItemTypeConstraint() );
		StashEquipment.AddConstraint( new OccupiedConstraint() );
	}

	public override void Simulate( Client cl )
	{
		base.Simulate( cl );

		//Controller.Simulate();
		Animator.Simulate();
		Agent.Simulate();

		HoveredEntity?.SetGlow( false );
		HoveredEntity = FindByIndex( (int)Input.Position.x );
		HoveredEntity?.SetGlow( true, Color.Red );

		SimulateInput();

		if ( IsClient )
		{
			var lr = Stats.Calculate( StatTypes.LightRadius );
			LightRadius.Brightness = 50f;
			LightRadius.OuterConeAngle = lr;
			LightRadius.InnerConeAngle = lr * .7f;
		}
	}

	public void OnItemAdded( StashEntity stash, Stashable item )
	{
		Host.AssertServer();

		if ( stash == StashEquipment )
		{
			AddItemAffixes( item );
		}
	}

	public void OnItemRemoved( StashEntity stash, Stashable item )
	{
		Host.AssertServer();

		if( stash == StashEquipment )
		{
			RemoveItemAffixes( item );
		}
	}

	private Dictionary<Stashable, List<int>> EquippedAffixes;
	private void AddItemAffixes( Stashable item ) 
	{
		Host.AssertServer();

		EquippedAffixes ??= new();

		if ( !EquippedAffixes.ContainsKey( item ) )
			EquippedAffixes.Add( item, new() );

		foreach ( var stat in item.ItemData.GetStats() )
		{
			var statId = Stats.Add( stat.Stat, stat.Modifier, stat.Amount );
			EquippedAffixes[item].Add( statId );
		}
	}

	private void RemoveItemAffixes( Stashable item )
	{
		Host.AssertServer();

		if ( !EquippedAffixes?.ContainsKey( item ) ?? false )
			return;

		foreach( var affix in EquippedAffixes[item] )
		{
			Stats.Remove( affix );
		}

		EquippedAffixes.Remove( item );
	}

}
