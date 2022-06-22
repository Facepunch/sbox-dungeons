﻿
// Jake:
//	1. Generate 1x1 grid
//	2. Randomly pick a cell
//	3. Merge it with a neighboring cell of the same size
//	4. Repeat steps 2 and 3 many times
//	5. Insert Nodes at start, end, and other points of interest
//	6. Pathfind between each node, only keeping cells along the paths

// Why not BSP tree?
//	This way makes it easy to parameterize dungeons with a graph
//	Give players a similar but randomized layout each playthrough
//	Anybody can create and adjust levels with a simple graphing tool

//	* = Node
//	
//	 * (START)
//	  \                    *--------*--------* (BOSS)
//	   \                  /         |
//      *----------------*          |
//                                  |
//                         *--------*
//                        /         |
//                       /          |
//           (DEAD END) *           * (SICK LOOT)

// Something I wanna experiment with:
//	Instead of merging cells randomly.. spawn a bunch of randomly sized
//	cells at the same position and use 2d physics to separate them

using Architect;
using Dungeons.Utility;
using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Dungeons;

internal partial class Dungeon : Entity
{

	[ConVar.Client( "debug_dungen" )]
	public static bool DebugDungen { get; set; }

	[Net]
	public int Seed { get; set; } = 1;

	public IReadOnlyList<DungeonCell> Cells => cells;
	public IReadOnlyList<DungeonRoute> Routes => routes;
	public IReadOnlyList<DungeonRoom> Rooms => rooms;

	private GridObject GridObject;
	private List<DungeonCell> cells = new();
	private List<DungeonRoute> routes = new();
	private List<DungeonRoom> rooms = new();
	private WallGeometry WallGeometry;

	public override void Spawn()
	{
		base.Spawn();

		Transmit = TransmitType.Always;
	}

	public override void ClientSpawn()
	{
		base.ClientSpawn();

		Generate();
	}

	// Customizable parameters, =>'s for now to test
	public int DungeonWidth => 8;
	public int DungeonHeight => 8;
	private int MaxCellWidth => 4;
	private int MaxCellHeight => 4;
	public int CellScale => 384;
	private int MergeIterations => 1024;
	private bool MergeHuggers => false;

	[Event.Hotload]
	private void Generate()
	{
		Rand.SetSeed( Seed );

		var mult = CellScale / 32;
		var gridSize = new Vector2( DungeonWidth * mult, DungeonHeight * mult );
		var mapBounds = new Vector3( DungeonWidth * CellScale, DungeonHeight * CellScale, 128f );

		WallGeometry?.Destroy();
		WallGeometry = new( gridSize, mapBounds );

		WallGeometry.hemesh.CreateGrid( DungeonWidth * mult, DungeonHeight * mult );
		WallGeometry.RebuildMesh();

		GridObject?.Destroy();
		GridObject = new( Map.Scene, Map.Physics, (DungeonWidth - 1) * mult, (DungeonHeight - 1) * mult );
		GridObject.Position = GridObject.Position.WithZ( -1 );

		rooms = new();
		routes = new();
		cells = CreateCells( DungeonWidth - 1, DungeonHeight - 1 );

		for ( int i = 0; i < MergeIterations; i++ )
		{
			var randomCell = Rand.FromList( cells );

			foreach ( var cell in cells )
			{
				if ( !CompatibleForMerge( randomCell, cell ) ) continue;

				randomCell.Rect.Add( cell.Rect );
				cells.RemoveAll( x => x != randomCell && randomCell.Rect.IsInside( x.Rect.Center ) );
				break;
			}
		}

		// add a few random routes for testing
		for ( int i = 0; i < 4; i++ )
		{
			var idx1 = Rand.Int( cells.Count - 1 );
			var idx2 = Rand.Int( cells.Count - 3 );
			var name1 = i == 0 ? "start" : (i == 1 ? "loot" : string.Empty);
			var name2 = i == 0 ? "end" : (i == 1 ? "boss" : string.Empty);
			cells[idx1].SetNode<DungeonNode>( name1 );
			cells[idx2].SetNode<DungeonNode>( name2 );
			routes.Add( new( this, new DungeonEdge( cells[idx1], cells[idx2] ) ) );
		}

		foreach ( var route in routes )
		{
			route.Calculate();
			foreach ( var cell in route.Cells )
			{
				if ( rooms.Any( x => x.Cell == cell ) )
					continue;

				rooms.Add( new( this, cell ) );
			}
		}

		foreach ( var room in rooms )
		{
			room.GenerateMesh( WallGeometry );
			WallGeometry.RebuildMesh();
		}
	}

	public List<DungeonCell> NeighborsOf( DungeonCell cell, bool orthogonal = true )
	{
		var result = new List<DungeonCell>();

		foreach ( var c in cells )
		{
			if ( !c.Rect.IsInside( cell.Rect ) ) continue;
			if ( orthogonal )
			{
				if ( c.Rect.BottomLeft == cell.Rect.TopRight ) continue;
				if ( c.Rect.BottomRight == cell.Rect.TopLeft ) continue;
				if ( c.Rect.TopLeft == cell.Rect.BottomRight ) continue;
				if ( c.Rect.TopRight == cell.Rect.BottomLeft ) continue;
			}
			result.Add( c );
		}

		return result;
	}

	private bool CompatibleForMerge( DungeonCell a, DungeonCell b )
	{
		var newrect = a.Rect;
		newrect.Add( b.Rect );

		if ( newrect.width > MaxCellWidth ) return false;
		if ( newrect.height > MaxCellHeight ) return false;

		if ( MergeHuggers )
		{
			// early true for rects that are same width or height AND hugging each other
			if ( a.Rect.left == b.Rect.left && a.Rect.width == b.Rect.width )
				if ( a.Rect.IsInside( b.Rect ) )
					return true;

			if ( a.Rect.bottom == b.Rect.bottom && a.Rect.height == b.Rect.height )
				if ( a.Rect.IsInside( b.Rect ) )
					return true;
		}

		if ( a.Rect.width != b.Rect.width ) return false;
		if ( a.Rect.height != b.Rect.height ) return false;
		if ( a.Rect.Position.DistanceOrtho( b.Rect.Position ) != 1 ) return false;

		return true;
	}

	private List<DungeonCell> CreateCells( int width, int height )
	{
		var result = new List<DungeonCell>();

		for ( int x = 0; x < width; x++ )
		{
			for ( int y = 0; y < height; y++ )
			{
				var pos = new Vector2( x, y );
				var rect = new Rect( pos, 1 );
				result.Add( new( rect ) );
			}
		}

		return result.OrderBy( x => Rand.Int( 999 ) ).ToList();
	}

	[Event.Frame]
	public void OnFrame()
	{
		if ( cells == null ) return;
		if ( !DebugDungen ) return;

		//WallGeometry?.DebugDraw();

		foreach ( var cell in cells )
		{
			var color = cell.Node != null ? Color.Green : Color.Black;
			var isroute = routes.Any( x => x.Cells.Contains( cell ) );
			var mins = new Vector3( cell.Rect.BottomLeft * CellScale, 1 );
			var maxs = new Vector3( cell.Rect.TopRight * CellScale, 1 );

			DebugOverlay.Box( mins, maxs, isroute ? Color.White : color.WithAlpha( .1f ) );

			if ( cell.Node == null ) continue;

			var center = new Vector3( cell.Rect.Center, 0 ) * CellScale;
			DebugOverlay.Text( cell.Node.Name, center, 0, 6000 );
			//DebugOverlay.Box( mins, maxs.WithZ( 256 ), color );
		}

		foreach ( var route in routes )
		{
			foreach ( var door in route.Doors )
			{
				var rect = door.CalculateRect();
				var mins = new Vector3( rect.BottomLeft * CellScale, 1 );
				var maxs = new Vector3( rect.TopRight * CellScale, 96 );
				//DebugOverlay.Circle( rect.Center * CellScale, Rotation.LookAt( Vector3.Up ), .15f * CellScale, Color.Cyan, 0, false );
			}

			for ( int i = 0; i < route.Doors.Count - 1; i++ )
			{
				var centera = route.Doors[i].CalculateRect().Center;
				var centerb = route.Doors[i + 1].CalculateRect().Center;
				DebugOverlay.Line( centera * CellScale, centerb * CellScale, 0, false );
			}
		}

	}

}
