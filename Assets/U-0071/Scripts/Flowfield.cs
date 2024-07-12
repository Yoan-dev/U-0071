using System;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	// used for wander flowfield init
	// (find loop in maze algo)
	public struct WandererComponent : IComponentData
	{
		public float2 Position;
		public float2 Direction;
	}

	public struct FlowfieldCell
	{
		// TODO: pack the float2s (several 8-directions in a byte)
		public float2 ToFood;
		public float2 ToWork;
		public float2 ToDestroy;
		public float2 ToProcess;
		public float2 ToWander;
		public float2 ToRelax;
	}

	public struct FlowfieldCollection : IComponentData, IDisposable
	{
		public Flowfield LevelOne;
		public Flowfield LevelTwo;
		public Flowfield LevelThree;
		public NativeArray<float2> ToRedAdmin;
		public NativeArray<float2> ToBlueAdmin;
		public NativeArray<float2> ToYellowAdmin;

		public FlowfieldCollection(int2 dimensions)
		{
			LevelOne = new Flowfield(dimensions);
			LevelTwo = new Flowfield(dimensions);
			LevelThree = new Flowfield(dimensions);
			ToRedAdmin = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
			ToBlueAdmin = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
			ToYellowAdmin = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			LevelOne.Dispose();
			LevelTwo.Dispose();
			LevelThree.Dispose();
			ToRedAdmin.Dispose();
			ToBlueAdmin.Dispose();
			ToYellowAdmin.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float2 GetDirection(AreaAuthorization areaFlag, AIGoal goal, float2 position)
		{
			return areaFlag switch
			{
				AreaAuthorization.LevelOne => LevelOne.GetDirection(goal, position),
				AreaAuthorization.LevelTwo => LevelTwo.GetDirection(goal, position),
				AreaAuthorization.LevelThree => LevelThree.GetDirection(goal, position),
				AreaAuthorization.Red => LevelThree.GetDirection(goal, position),
				AreaAuthorization.Blue => LevelThree.GetDirection(goal, position),
				AreaAuthorization.Yellow => LevelThree.GetDirection(goal, position),
				_ => float2.zero,
			};
		}
	}

	public struct Flowfield : /*IComponentData, ISharedComponentData,*/IDisposable
	{
		// TBD: could be a component shared by same-authorization entities ?

		public NativeArray<FlowfieldCell> Cells;
		public int2 Dimensions;

		public Flowfield(int2 dimensions)
		{
			Dimensions = dimensions;
			Cells = new NativeArray<FlowfieldCell>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			Cells.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float2 GetDirection(AIGoal goal, float2 position)
		{
			FlowfieldCell cell = GetCell(position);
			return goal switch
			{
				AIGoal.Eat => cell.ToFood,
				AIGoal.Work => cell.ToWork,
				AIGoal.Destroy => cell.ToDestroy,
				AIGoal.Process => cell.ToProcess,
				AIGoal.Wander => cell.ToWander,
				AIGoal.Relax => cell.ToRelax,
				AIGoal.Flee => cell.ToRelax.Equals(float2.zero) ? cell.ToWander : cell.ToRelax,
				_ => float2.zero,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FlowfieldCell GetCell(float2 position)
		{
			int index = GetIndex(position);
			return index >= 0 && index < Cells.Length ? Cells[index] : new FlowfieldCell();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetIndex(float2 position)
		{
			return (int)(position.x + Dimensions.x / 2) + (int)(position.y + Dimensions.y / 2) * Dimensions.x;
		}
	}

	public struct FlowfieldBuilderCell
	{
		public float2 Position;
		public AreaAuthorization AreaFlags;
		public AreaAuthorization LowestAreaFlag;
		public int Index;
		public int Value;
		public bool Pathable;
		public bool HasObstacle; // rough "cost"
	}

	public struct FlowfieldBuilder : IDisposable
	{
		public NativeArray<float2> Flowfield;
		public NativeArray<FlowfieldBuilderCell> Cells;
		public NativeQueue<FlowfieldBuilderCell> Queue;
		public AreaAuthorization AreaFlag;
		public ActionFlag ActionFlag;
		public ItemFlag ItemFlag;
		public bool WorkFlag;
		public int2 Dimensions;

		private int offsetNW => Dimensions.x - 1;
		private int offsetN => Dimensions.x;
		private int offsetNE => Dimensions.x + 1;
		private int offsetSW => -Dimensions.x - 1;
		private int offsetS => -Dimensions.x;
		private int offsetSE => -Dimensions.x + 1;

		public FlowfieldBuilder(AreaAuthorization areaFlag, ActionFlag actionFlag, ItemFlag itemFlag, in Partition partition, bool workFlag = false)
		{
			// lone flag because we will do < comparisons
			AreaFlag = areaFlag;

			Dimensions = partition.Dimensions;
			ActionFlag = actionFlag;
			ItemFlag = itemFlag;
			WorkFlag = workFlag;
			Queue = new NativeQueue<FlowfieldBuilderCell>(Allocator.TempJob);
			Flowfield = new NativeArray<float2>(Dimensions.x * Dimensions.y, Allocator.TempJob);
			Cells = new NativeArray<FlowfieldBuilderCell>(Dimensions.x * Dimensions.y, Allocator.TempJob);
			for (int i = 0; i < Cells.Length; i++)
			{
				AreaAuthorization areaFlags = partition.GetAuthorization(i);
				Cells[i] = new FlowfieldBuilderCell
				{
					Position = new float2(i % Dimensions.x - Dimensions.x / 2, i / Dimensions.x - Dimensions.y / 2),
					AreaFlags = areaFlags,
					LowestAreaFlag = Utilities.GetLowestAuthorization(areaFlags),
					Index = i,
					Value = int.MaxValue,
					Pathable = partition.IsPathable(i),
				};
			}
		}

		public void Dispose()
		{
			Flowfield.Dispose();
			Cells.Dispose();
			Queue.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ProcessDevice(in InteractableComponent interactable, in Partition partition, float2 position, int size)
		{
			// flowfield starts from:
			// - same area working stations if work flag
			// - devices with no action/item flags or required ones
			// - authorized areas
			int index = GetIndex(position);
			AreaAuthorization lowestCellAuthorization = Utilities.GetLowestAuthorization(partition.GetRoomData(index).AreaFlag);
			bool isStartingCell =
				(!WorkFlag || (interactable.WorkingStationFlag && AreaFlag == lowestCellAuthorization)) &&
				(ActionFlag == 0 || interactable.HasActionFlag(ActionFlag)) &&
				(ItemFlag == 0 || interactable.HasItemFlag(ItemFlag)) &&
				Utilities.CompareAuthorization(AreaFlag, lowestCellAuthorization);

			if (size == 1)
			{
				InitStartingCell(index, isStartingCell);
			}
			else
			{
				for (int y = 0; y < size; y++)
				{
					for (int x = 0; x < size; x++)
					{
						InitStartingCell(GetIndex(new float2(position.x + x - size / 2f, position.y + y - size / 2f)), isStartingCell);
					}
				}
			}
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void InitStartingCell(int index, bool isStartingCell)
		{
			FlowfieldBuilderCell cell = Cells[index];
			if (isStartingCell)
			{
				cell.Value = 0;
				Queue.Enqueue(cell);
			}
			else
			{
				cell.HasObstacle = true;
			}
			Cells[index] = cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Spread()
		{
			while (Queue.Count > 0)
			{
				FlowfieldBuilderCell cell = Queue.Dequeue();

				FlowfieldBuilderCell cellNW = GetCell(cell.Index + offsetNW);
				FlowfieldBuilderCell cellN = GetCell(cell.Index + offsetN);
				FlowfieldBuilderCell cellNE = GetCell(cell.Index + offsetNE);
				FlowfieldBuilderCell cellW = GetCell(cell.Index - 1);
				FlowfieldBuilderCell cellE = GetCell(cell.Index + 1);
				FlowfieldBuilderCell cellSW = GetCell(cell.Index + offsetSW);
				FlowfieldBuilderCell cellS = GetCell(cell.Index + offsetS);
				FlowfieldBuilderCell cellSE = GetCell(cell.Index + offsetSE);

				// adjacents
				TryEnqueueCell(in cell, cellN);
				TryEnqueueCell(in cell, cellS);
				TryEnqueueCell(in cell, cellE);
				TryEnqueueCell(in cell, cellW);

				// diagonals
				if (cellN.Pathable && cellW.Pathable) TryEnqueueCell(in cell, cellNW);
				if (cellN.Pathable && cellE.Pathable) TryEnqueueCell(in cell, cellNE);
				if (cellS.Pathable && cellW.Pathable) TryEnqueueCell(in cell, cellSW);
				if (cellS.Pathable && cellE.Pathable) TryEnqueueCell(in cell, cellSE);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TryEnqueueCell(in FlowfieldBuilderCell cell, FlowfieldBuilderCell checkedCell)
		{
			// flow can go from low to high authorization (unset value) but not the opposite,
			// except if we start from high authorization (flowfield area flag)
			// allows for all cells to have a value (can go back to authorized if lost somehow)
			// but never path to an un-authorized cell (in the case of a loop / go through room)
			if (checkedCell.Pathable && checkedCell.Value > cell.Value + 1 && (
				Utilities.CompareAuthorization(checkedCell.LowestAreaFlag, cell.LowestAreaFlag) && checkedCell.Value == int.MaxValue ||
				Utilities.CompareAuthorization(AreaFlag, checkedCell.LowestAreaFlag) && Utilities.HasAuthorization(cell.AreaFlags, AreaFlag))) 
			{
				checkedCell.Value = cell.Value + 1;
				Cells[checkedCell.Index] = checkedCell;
				Queue.Enqueue(checkedCell);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ProcessDirection(int index)
		{
			FlowfieldBuilderCell cell = Cells[index];

			if (!cell.Pathable) return;

			FlowfieldBuilderCell cellNW = GetCell(cell.Index + offsetNW);
			FlowfieldBuilderCell cellN = GetCell(cell.Index + offsetN);
			FlowfieldBuilderCell cellNE = GetCell(cell.Index + offsetNE);
			FlowfieldBuilderCell cellW = GetCell(cell.Index - 1);
			FlowfieldBuilderCell cellE = GetCell(cell.Index + 1);
			FlowfieldBuilderCell cellSW = GetCell(cell.Index + offsetSW);
			FlowfieldBuilderCell cellS = GetCell(cell.Index + offsetS);
			FlowfieldBuilderCell cellSE = GetCell(cell.Index + offsetSE);

			FlowfieldBuilderCell best = cell;

			// adjacents
			TryGetBest(ref best, in cellN);
			TryGetBest(ref best, in cellS);
			TryGetBest(ref best, in cellE);
			TryGetBest(ref best, in cellW);

			// diagonals
			if (cellN.Pathable && cellW.Pathable) TryGetBest(ref best, in cellNW);
			if (cellN.Pathable && cellE.Pathable) TryGetBest(ref best, in cellNE);
			if (cellS.Pathable && cellW.Pathable) TryGetBest(ref best, in cellSW);
			if (cellS.Pathable && cellE.Pathable) TryGetBest(ref best, in cellSE);

			Flowfield[index] = math.normalizesafe(best.Position - cell.Position);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TryGetBest(ref FlowfieldBuilderCell currentBest, in FlowfieldBuilderCell checkedCell)
		{
			if (checkedCell.Value < currentBest.Value || checkedCell.Value == currentBest.Value && !checkedCell.HasObstacle && currentBest.HasObstacle)
			{
				currentBest = checkedCell;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private FlowfieldBuilderCell GetCell(int index)
		{
			return index >= 0 && index < Cells.Length ? Cells[index] : new FlowfieldBuilderCell { Value = int.MaxValue, Pathable = false };
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetIndex(float2 position)
		{
			return (int)(position.x + Dimensions.x / 2) + (int)(position.y + Dimensions.y / 2) * Dimensions.x;
		}
	}
}