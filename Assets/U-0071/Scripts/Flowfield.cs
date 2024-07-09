using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct Flowfield : IComponentData, IDisposable
	{
		public NativeArray<float2> FoodLevelZero;
		public NativeArray<float2> DestroyLevelZero;
		public int2 Dimensions;

		public Flowfield(int2 dimensions)
		{
			Dimensions = dimensions;
			FoodLevelZero = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
			DestroyLevelZero = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			FoodLevelZero.Dispose();
			DestroyLevelZero.Dispose();
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
		public int2 Dimensions;

		private int offsetNW => Dimensions.x - 1;
		private int offsetN => Dimensions.x;
		private int offsetNE => Dimensions.x + 1;
		private int offsetSW => -Dimensions.x - 1;
		private int offsetS => -Dimensions.x;
		private int offsetSE => -Dimensions.x + 1;

		public FlowfieldBuilder(NativeArray<float2> flowfield, ActionFlag actionFlag, ItemFlag itemFlag, in Partition partition)
		{
			Flowfield = flowfield;
			Dimensions = partition.Dimensions;
			ActionFlag = actionFlag;
			ItemFlag = itemFlag;
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
			bool isStartingCell = interactable.HasActionFlag(ActionFlag) && interactable.HasItemFlag(ItemFlag);

			if (size == 1)
			{
				InitDeviceCell(partition.GetIndex(position), isStartingCell);
			}
			else
			{
				for (int y = 0; y < size; y++)
				{
					for (int x = 0; x < size; x++)
					{
						InitDeviceCell(partition.GetIndex(new float2(position.x + x - size / 2f, position.y + y - size / 2f)), isStartingCell);
					}
				}
			}
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void InitDeviceCell(int index, bool isStartingCell)
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
				FlowfieldBuilderCell cellNW = Cells[cell.Index + offsetNW];
				FlowfieldBuilderCell cellN = Cells[cell.Index + offsetN];
				FlowfieldBuilderCell cellNE = Cells[cell.Index + offsetNE];
				FlowfieldBuilderCell cellW = Cells[cell.Index - 1];
				FlowfieldBuilderCell cellE = Cells[cell.Index + 1];
				FlowfieldBuilderCell cellSW = Cells[cell.Index + offsetSW];
				FlowfieldBuilderCell cellS = Cells[cell.Index + offsetS];
				FlowfieldBuilderCell cellSE = Cells[cell.Index + offsetSE];

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
			if (checkedCell.Pathable && checkedCell.Value > cell.Value)
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

			FlowfieldBuilderCell cellNW = GetCellSafe(cell.Index + offsetNW);
			FlowfieldBuilderCell cellN = GetCellSafe(cell.Index + offsetN);
			FlowfieldBuilderCell cellNE = GetCellSafe(cell.Index + offsetNE);
			FlowfieldBuilderCell cellW = GetCellSafe(cell.Index - 1);
			FlowfieldBuilderCell cellE = GetCellSafe(cell.Index + 1);
			FlowfieldBuilderCell cellSW = GetCellSafe(cell.Index + offsetSW);
			FlowfieldBuilderCell cellS = GetCellSafe(cell.Index + offsetS);
			FlowfieldBuilderCell cellSE = GetCellSafe(cell.Index + offsetSE);

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
		private FlowfieldBuilderCell GetCellSafe(int index)
		{
			return index >= 0 && index < Cells.Length ? Cells[index] : new FlowfieldBuilderCell { Value = int.MaxValue };
		}
	}
}