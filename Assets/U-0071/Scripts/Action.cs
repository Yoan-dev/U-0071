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
		All = Pick | Grind,
	}

	public struct ActionTarget
	{
		public float2 Position;
		public Entity Target;
		public ActionType Type;
		public float Range;

		public bool Has => Target != Entity.Null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionTarget(Entity target, ActionType type, float2 position, float range)
		{
			Target = target;
			Type = type;
			Position = position;
			Range = range;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasType(ActionType inType)
		{
			return Utilities.HasActionType(Type, inType);
		}
	}

	public struct InteractableComponent : IComponentData
	{
		public float Range;
		public ActionType Flags;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasType(ActionType inType)
		{
			return Utilities.HasActionType(Flags, inType);
		}
	}

	public struct PickComponent : IComponentData, IEnableableComponent
	{
		public Entity Picked;
	}

	public struct PickableComponent : IComponentData, IEnableableComponent
	{
		public Entity Carrier;
	}

	[InternalBufferCapacity(0)]
	public struct ActionEventBufferElement : IBufferElementData
	{
		public ActionTarget Action;
		public Entity Source;

		public Entity Target => Action.Target;
		public ActionType Type => Action.Type;
		public float2 Position => Action.Position;
	}
}