using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct WandererComponent : IComponentData
	{
		public float2 Position;
		public float2 Direction;
	}

	public struct Flowfield : IComponentData, IDisposable
	{
		// TODO: per autorisation level
		// TODO: ensure loop in every area for wander
		public NativeArray<float2> FoodLevelZero;
		public NativeArray<float2> WorkLevelZero;
		public NativeArray<float2> Destroy;
		public NativeArray<float2> Wander;
		public int2 Dimensions;

		public Flowfield(int2 dimensions)
		{
			Dimensions = dimensions;
			FoodLevelZero = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
			Destroy = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
			WorkLevelZero = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
			Wander = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			FoodLevelZero.Dispose();
			WorkLevelZero.Dispose();
			Destroy.Dispose();
			Wander.Dispose();
		}

		public float2 GetDirection(AIGoal goal, float2 position)
		{
			int index = GetIndex(position);
			return 
				goal == AIGoal.Eat ? FoodLevelZero[index] : 
				goal == AIGoal.Destroy ? Destroy[index] :
				goal == AIGoal.Work ? WorkLevelZero[index] : 
				goal == AIGoal.Wander || goal == AIGoal.Flee ? Wander[index] : float2.zero;
			
			// TODO: flee field usage (go to their level)
			// TODO: the rest / autorisation level
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

		public FlowfieldBuilder(NativeArray<float2> flowfield, ActionFlag actionFlag, ItemFlag itemFlag, in Partition partition, bool workFlag = false)
		{
			Flowfield = flowfield;
			Dimensions = partition.Dimensions;
			ActionFlag = actionFlag;
			ItemFlag = itemFlag;
			WorkFlag = workFlag;
			Queue = new NativeQueue<FlowfieldBuilderCell>(Allocator.Persistent);
			Cells = new NativeArray<FlowfieldBuilderCell>(flowfield.Length, Allocator.Persistent);
			for (int i = 0; i < Cells.Length; i++)
			{
				Cells[i] = new FlowfieldBuilderCell
				{
					Position = new float2(i % Dimensions.x, i / Dimensions.x), // only need relative pos to each other
					Index = i,
					Value = int.MaxValue,
					Pathable = partition.IsPathable(i),
				};
			}
		}

		public void Dispose()
		{
			Cells.Dispose();
			Queue.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ProcessDevice(in InteractableComponent interactable, in Partition partition, float2 position, int size)
		{
			bool isStartingCell =
				(!WorkFlag || interactable.WorkingStationFlag) && 
				(ActionFlag == 0 || interactable.HasActionFlag(ActionFlag)) && 
				(ItemFlag == 0 || interactable.HasItemFlag(ItemFlag));

			if (size == 1)
			{
				InitStartingCell(partition.GetIndex(position), isStartingCell);
			}
			else
			{
				for (int y = 0; y < size; y++)
				{
					for (int x = 0; x < size; x++)
					{
						InitStartingCell(partition.GetIndex(new float2(position.x + x - size / 2f, position.y + y - size / 2f)), isStartingCell);
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
			else // obstacle
			{
				cell.HasObstacle = true;
			}
			Cells[index] = cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Spread()
		{
			// note: no check for out of bound index
			// (map will have buffer around rooms)

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
			if (checkedCell.Pathable && checkedCell.Value > cell.Value + 1)
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