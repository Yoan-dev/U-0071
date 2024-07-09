using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[Flags]
	public enum ActionType
	{
		// sorted by priority
		Eat = 1 << 0,
		Push = 1 << 1,
		Pick = 1 << 2,
		Search = 1 << 3,
		Collect = 1 << 4,
		Destroy = 1 << 5,
		Store = 1 << 6,
		Drop = 1 << 7,
		Process = 1 << 8,
		
		// companion-flags (ex: Collect + RefEat => buy meal)
		RefTrash = 1 << 9,
		RefProcess = 1 << 10,
		RefEat = 1 << 11,
	}

	public struct ActionData
	{
		public float2 Position;
		public Entity Target;
		public ActionType Type;
		public float Range;
		public float Time;
		public int Cost;

		public bool Has => Target != Entity.Null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionData(Entity target, ActionType type, float2 position, float range, float time, int cost)
		{
			Target = target;
			Type = type;
			Position = position;
			Range = range;
			Time = time;
			Cost = cost;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasActionType(ActionType inType)
		{
			return Utilities.HasActionType(Type, inType);
		}
	}

	public struct ActionController : IComponentData
	{
		public ActionData Action;
		public float Timer;
		public bool IsResolving;

		public bool HasTarget => Action.Has;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Start()
		{
			Timer = 0f;
			IsResolving = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Stop()
		{
			Action.Target = Entity.Null;
			Action.Type = 0;
			IsResolving = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ShouldResolve(int credits)
		{
			// TBC credits check, should already be checked in controller (action start)
			return Timer >= Action.Time && (Action.Cost <= 0f || Action.Cost <= credits);
		}
	}

	public struct IsActing : IComponentData, IEnableableComponent { }

	public struct InteractableComponent : IComponentData
	{
		public ActionType Flags;
		public float Range;
		public float Time;
		public int Cost;
		public bool Changed;

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
		public ActionType Flags;
		public float Time;
	}

	public struct PickableComponent : IComponentData, IEnableableComponent
	{
		public Entity Carrier;
		public float CarriedZOffset;
	}
}