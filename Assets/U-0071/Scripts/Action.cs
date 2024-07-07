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
		Use = 1 << 2,
		All = Pick | Use,
	}

	public struct ActionTarget
	{
		public float2 Position;
		public Entity Target;
		public ActionType Type;

		public bool Has => Target != Entity.Null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionTarget(Entity target, ActionType type, float2 position)
		{
			Target = target;
			Type = type;
			Position = position;
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
	public struct PickDropEventBufferElement : IBufferElementData
	{
		public ActionTarget Action;
		public Entity Source;
	}
}