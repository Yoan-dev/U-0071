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

	public struct InteractableComponent : IComponentData
	{
		public ActionType Type;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsType(ActionType inType)
		{
			return Utilities.IsActionType(Type, inType);
		}
	}

	public struct PickComponent : IComponentData, IEnableableComponent
	{
		public Entity Picked;
	}

	public struct PickedComponent : IComponentData, IEnableableComponent
	{
		public Entity Carrier;
	}

	[InternalBufferCapacity(0)]
	public struct ActionEventBufferElement : IBufferElementData
	{
		public float2 Position;
		public Entity Source;
		public Entity Target;
		public ActionType Type;
	}
}