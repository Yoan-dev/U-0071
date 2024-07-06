using System;
using System.Runtime.CompilerServices;
using Unity.Entities;

namespace U0071
{
	[Flags]
	public enum ActionType
	{
		Pick = 1 << 0,
		Drop = 1 << 1,
	}

	public struct InteractableComponent : IComponentData
	{
		public ActionType Type;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsType(ActionType inType)
		{
			return Utilities.IsActionType(Type, inType);
		}
	}

	public struct PickComponent : IComponentData
	{
		public Entity Picked;
	}

	public struct PickedTag : IComponentData, IEnableableComponent { }

	[InternalBufferCapacity(0)]
	public struct ActionEventBufferElement : IBufferElementData
	{
		public Entity Source;
		public Entity Target;
		public ActionType Type;
	}
}