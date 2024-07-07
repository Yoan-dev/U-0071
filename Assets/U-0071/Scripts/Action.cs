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
	}

	public struct ActionTarget
	{
		public float2 Position;
		public Entity Target;
		public ActionType Type;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionTarget(Entity target, ActionType type, float2 position)
		{
			Target = target;
			Type = type;
			Position = position;
		}
	}

	public struct ActionController : IComponentData
	{
		public ActionTarget Primary;
		public ActionTarget Secondary;

		public bool HasPrimaryAction => Primary.Target != Entity.Null;
		public bool HasSecondaryAction => Secondary.Target != Entity.Null;
	}

	public struct InteractableComponent : IComponentData
	{
		public ActionType Flags;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsType(ActionType inType)
		{
			return Utilities.IsActionType(Flags, inType);
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
		public ActionTarget Target;
		public Entity Source;
	}
}