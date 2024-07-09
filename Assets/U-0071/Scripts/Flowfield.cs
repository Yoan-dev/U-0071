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
		public int Index;
		public int Value;
		public bool Pathable;
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
			Queue = new NativeQueue<FlowfieldBuilderCell>(Allocator.Persistent);
			Cells = new NativeArray<FlowfieldBuilderCell>(flowfield.Length, Allocator.Persistent);
			for (int i = 0; i < Cells.Length; i++)
			{
				Cells[i] = new FlowfieldBuilderCell
				{
					Index = i,
					Value = int.MaxValue,
					Pathable = partition.IsPathable(i),
				};
			}
			ActionFlag = actionFlag;
			ItemFlag = itemFlag;
			Dimensions = partition.Dimensions;
		}

		public void Dispose()
		{
			Cells.Dispose();
			Queue.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ProcessDevice(in InteractableComponent interactable, in Partition partition, float2 position, int size)
		{
			if (interactable.HasActionFlag(ActionFlag) && interactable.HasItemFlag(ItemFlag))
			{
				if (size == 1)
				{
					AddStartingCell(partition.GetIndex(position));
				}
				else
				{
					for (int y = 0; y < size; y++)
					{
						for (int x = 0; x < size; x++)
						{
							AddStartingCell(partition.GetIndex(new float2(position.x + x - size / 2f, position.y + y - size / 2f)));
						}
					}
				}
			}
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddStartingCell(int index)
		{
			FlowfieldBuilderCell cell = Cells[index];
			cell.Value = 0;
			Cells[index] = cell;
			Queue.Enqueue(cell);
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
			Flowfield[index] = math.normalizesafe(GetDirection(index));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float2 GetDirection(int index)
		{
			FlowfieldBuilderCell cell = Cells[index];
			FlowfieldBuilderCell cellNW = GetCellSafe(cell.Index + offsetNW);
			FlowfieldBuilderCell cellN = GetCellSafe(cell.Index + offsetN);
			FlowfieldBuilderCell cellNE = GetCellSafe(cell.Index + offsetNE);
			FlowfieldBuilderCell cellW = GetCellSafe(cell.Index - 1);
			FlowfieldBuilderCell cellE = GetCellSafe(cell.Index + 1);
			FlowfieldBuilderCell cellSW = GetCellSafe(cell.Index + offsetSW);
			FlowfieldBuilderCell cellS = GetCellSafe(cell.Index + offsetS);
			FlowfieldBuilderCell cellSE = GetCellSafe(cell.Index + offsetSE);

			// adjacents
			if (cell.Value > cellN.Value) return new float2(0f, 1f);
			if (cell.Value > cellS.Value) return new float2(0f, -1f);
			if (cell.Value > cellE.Value) return new float2(1f, 0f);
			if (cell.Value > cellW.Value) return new float2(-1f, 0f);

			// diagonals
			if (cellN.Pathable && cellW.Pathable && cell.Value > cellNW.Value) return new float2(-1f, 1f);
			if (cellN.Pathable && cellE.Pathable && cell.Value > cellNE.Value) return new float2(1f, 1f);
			if (cellS.Pathable && cellW.Pathable && cell.Value > cellSW.Value) return new float2(-1f, -1f);
			if (cellS.Pathable && cellE.Pathable && cell.Value > cellSE.Value) return new float2(1f, -1f);

			return float2.zero;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private FlowfieldBuilderCell GetCellSafe(int index)
		{
			return index >= 0 && index < Cells.Length ? Cells[index] : new FlowfieldBuilderCell { Value = int.MaxValue };
		}
	}
}