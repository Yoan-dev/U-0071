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

	public struct FlowfieldBuilder : IDisposable
	{
		public NativeArray<float2> Flowfield;
		public NativeArray<int> Values;
		public NativeQueue<int> Queue;
		public ActionFlag ActionFlag;
		public ItemFlag ItemFlag;

		public FlowfieldBuilder(NativeArray<float2> flowfield, ActionFlag actionFlag, ItemFlag itemFlag)
		{
			Flowfield = flowfield;
			Values = new NativeArray<int>(flowfield.Length, Allocator.Persistent);
			Queue = new NativeQueue<int>(Allocator.Persistent);
			for (int i = 0; i < Values.Length; i++)
			{
				Values[i] = int.MaxValue;
			}
			ActionFlag = actionFlag;
			ItemFlag = itemFlag;
		}

		public void Dispose()
		{
			Values.Dispose();
			Queue.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ProcessDevice(in InteractableComponent interactable, in Partition partition, float2 position, int size)
		{
			if (interactable.HasActionFlag(ActionFlag) && interactable.HasItemFlag(ItemFlag))
			{
				if (size == 1)
				{
					Values[partition.GetIndex(position)] = 0;
				}
				else
				{
					for (int y = 0; y < size; y++)
					{
						for (int x = 0; x < size; x++)
						{
							Values[partition.GetIndex(new float2(position.x + x - size / 2f, position.y + y - size / 2f))] = 0;
						}
					}
				}
			}
		}
	}
}