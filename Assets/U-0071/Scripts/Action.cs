using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[Flags]
	public enum ActionFlag
	{
		// sorted by priority
		Drop = 1 << 0,
		Eat = 1 << 1,
		Push = 1 << 2,
		Pick = 1 << 3,
		Search = 1 << 4,
		Collect = 1 << 5,
		Destroy = 1 << 6,
		Store = 1 << 7,
		Teleport = 1 << 8,
	}

	public struct ActionData
	{
		public float2 Position;
		public Entity Target;
		public ActionFlag ActionFlag;
		public ItemFlag ItemFlags;
		public ItemFlag UsedItemFlags;
		public float Range;
		public float Time;
		public int Cost;

		public bool Has => Target != Entity.Null;

		// hard-coded
		public bool UseVariantSpawn => 
			ActionFlag == ActionFlag.Store &&
			Utilities.HasItemFlag(ItemFlags, ItemFlag.RawFood) &&
			Utilities.HasItemFlag(UsedItemFlags, ItemFlag.Trash);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionData(Entity target, ActionFlag actionFlag, ItemFlag itemFlags, ItemFlag usedItemFlags, float2 position, float range, float time, int cost)
		{
			Target = target;
			ActionFlag = actionFlag;
			ItemFlags = itemFlags;
			UsedItemFlags = usedItemFlags;
			Position = position;
			Range = range;
			Time = time;
			Cost = cost;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasActionFlag(ActionFlag inFlag)
		{
			return Utilities.HasActionFlag(ActionFlag, inFlag);
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
			Action.ActionFlag = 0;
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
		public ActionFlag ActionFlags;
		public ItemFlag ItemFlags; // type for items, requirement for devices
		public float Range;
		public float Time;
		public float CollisionRadius; // cached here to have access in partition (meh)
		public int Cost;
		public bool Changed;
		public bool WorkingStationFlag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasActionFlag(ActionFlag inFlag)
		{
			return Utilities.HasActionFlag(ActionFlags, inFlag);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasItemFlag(ItemFlag inFlag)
		{
			return Utilities.HasItemFlag(ItemFlags, inFlag);
		}
	}

	public struct CarryComponent : IComponentData, IEnableableComponent
	{
		public float2 Position;
		public Entity Picked;
		public ItemFlag Flags;
		public float YOffset;
		public float Time;

		public bool HasItem => Picked != Entity.Null;
	}

	public struct PickableComponent : IComponentData, IEnableableComponent
	{
		public Entity Carrier;
		public float CarriedZOffset; // extra for corpses
	}
}