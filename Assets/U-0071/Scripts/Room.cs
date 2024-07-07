using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct PartitionComponent : IComponentData
	{
		public Entity CurrentRoom;
	}

	public struct RoomComponent : IComponentData
	{
		public int2 Dimensions;
	}

	[InternalBufferCapacity(32)]
	public struct RoomElementBufferElement : IBufferElementData
	{
		public float2 Position;
		public Entity Entity;
		public ActionType ActionType;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomElementBufferElement(Entity entity, float2 position, ActionType actionType)
		{
			Entity = entity;
			Position = position;
			ActionType = actionType;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomElementBufferElement(Entity entity)
		{
			Entity = entity;
			Position = float2.zero;
			ActionType = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsActionType(ActionType inType)
		{
			return Utilities.HasActionType(ActionType, inType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void RemoveElement(ref DynamicBuffer<RoomElementBufferElement> elements, in RoomElementBufferElement element)
		{
			// reverse search because moving entities have more chance to be at the back
			for (int i = elements.Length - 1; i >= 0; i--)
			{
				if (elements[i].Entity == element.Entity)
				{
					elements.RemoveAtSwapBack(i);
					break;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void UpdateElement(ref DynamicBuffer<RoomElementBufferElement> elements, in RoomElementBufferElement element)
		{
			// reverse search because moving entities have more chance to be at the back
			for (int i = elements.Length - 1; i >= 0; i--)
			{
				if (elements[i].Entity == element.Entity)
				{
					elements[i] = element;
					break;
				}
			}
		}
	}

	[InternalBufferCapacity(4)]
	public struct RoomLinkBufferElement : IBufferElementData
	{
		public int2 Anchor;
		public Entity Neighbor;
		public bool IsOpen;
	}

	public struct RoomPartition : IComponentData, IDisposable
	{
		public NativeArray<Entity> Cells; // cell to room
		public int2 Dimensions;

		public RoomPartition(int2 dimensions)
		{
			Dimensions = dimensions;
			Cells = new NativeArray<Entity>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			Cells.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Entity GetRoom(float2 position)
		{
			int index = GetIndex(position);
			return index >= 0 && index < Cells.Length ? Cells[index] : Entity.Null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetRoom(Entity room, float2 position)
		{
			int index = GetIndex(position);
			if (index >= 0 && index < Cells.Length)
			{
				Cells[index] = room;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetIndex(float2 position)
		{
			return (int)(position.x + Dimensions.x / 2) + (int)(position.y + Dimensions.y / 2) * Dimensions.x;
		}
	}
}