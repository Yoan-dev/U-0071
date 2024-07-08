using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[Flags]
	public enum ActionType
	{
		Pick = 1 << 0,
		Drop = 1 << 1,
		Grind = 1 << 2,
		Buy = 1 << 3,
		All = Pick | Grind | Buy,
	}

	public struct ActionData
	{
		public float2 Position;
		public Entity Target;
		public ActionType Type;
		public float Range;
		public int Cost;

		public bool Has => Target != Entity.Null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionData(Entity target, ActionType type, float2 position, float range, int cost)
		{
			Target = target;
			Type = type;
			Position = position;
			Range = range;
			Cost = cost;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasType(ActionType inType)
		{
			return Utilities.HasActionType(Type, inType);
		}
	}

	public struct InteractableComponent : IComponentData
	{
		public ActionType Flags;
		public float Range;
		public int Cost;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasType(ActionType inType)
		{
			return Utilities.HasActionType(Flags, inType);
		}
	}

	public struct PickComponent : IComponentData, IEnableableComponent
	{
		public float2 Position;
		public Entity Picked;
		public float YOffset;
	}

	public struct PickableComponent : IComponentData, IEnableableComponent
	{
		public Entity Carrier;
	}

	[InternalBufferCapacity(0)]
	public struct ActionEventBufferElement : IBufferElementData
	{
		public ActionData Action;
		public Entity Source;

		public Entity Target => Action.Target;
		public ActionType Type => Action.Type;
		public float2 Position => Action.Position;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionEventBufferElement(Entity source, in ActionData action)
		{
			Source = source;
			Action = action;
		}
	}
}