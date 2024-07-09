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

	public struct RoomData
	{
		public RoomComponent Room;
		public float2 Position;
		public Entity Entity;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float2 GetRoomRatio(float2 position)
		{
			return new float2
			{
				x = (position.x - (Position.x - Room.Dimensions.x / 2f)) / Room.Dimensions.x,
				y = -(position.y - (Position.y + Room.Dimensions.y / 2f)) / Room.Dimensions.y,
			};
		}
	}

	[InternalBufferCapacity(32)]
	public struct RoomElementBufferElement : IBufferElementData
	{
		public Entity Entity;
		public float2 Position;
		public InteractableComponent Interactable; // cached

		public ActionType ActionFlags => Interactable.Flags;
		public float Range => Interactable.Range;
		public float Time => Interactable.Time;
		public int Cost => Interactable.Cost;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionData ToActionData(ActionType selectedActionType)
		{
			return new ActionData(Entity, selectedActionType, Position, Range, Const.GetActionTime(selectedActionType, Time), Cost);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomElementBufferElement(Entity entity, float2 position, in InteractableComponent interactable)
		{
			Entity = entity;
			Position = position;
			Interactable = interactable;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomElementBufferElement(Entity entity)
		{
			Entity = entity;
			Position = float2.zero;
			Interactable = new InteractableComponent();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Evaluate(ActionType current, ActionType filter, bool isCarryingItem, out ActionType selected)
		{
			// TODO: send carried item flags

			// retrieve eligible action in priority order
			return
				EvaluateActionType(ActionType.Store, current, filter, isCarryingItem, out selected) ||
				EvaluateActionType(ActionType.Destroy, current, filter, isCarryingItem, out selected) ||
				EvaluateActionType(ActionType.Collect, current, filter, isCarryingItem, out selected) ||
				EvaluateActionType(ActionType.Search, current, filter, isCarryingItem, out selected) ||
				EvaluateActionType(ActionType.Pick, current, filter, isCarryingItem, out selected) ||
				EvaluateActionType(ActionType.Push, current, filter, isCarryingItem, out selected);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateActionType(ActionType checkedType, ActionType current, ActionType filter, bool isCarryingItem, out ActionType selected)
		{
			selected =
				checkedType >= current && // >= to search closest
				(ActionFlags & filter & checkedType) != 0 &&
				(isCarryingItem || !Utilities.RequireCarriedItem(checkedType)) ? checkedType : 0;
			return selected != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasActionType(ActionType inType)
		{
			return Utilities.HasActionType(ActionFlags, inType);
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
		public NativeArray<RoomData> Cells; // cell to room
		public int2 Dimensions;

		public RoomPartition(int2 dimensions)
		{
			Dimensions = dimensions;
			Cells = new NativeArray<RoomData>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			Cells.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomData GetRoomData(float2 position)
		{
			int index = GetIndex(position);
			return index >= 0 && index < Cells.Length ? Cells[index] : new RoomData();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetRoomData(RoomData room, float2 position)
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