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
		Open = 1 << 9,
		Administrate = 1 << 10,
		Contaminate = 1 << 11,
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
		public bool MultiusableFlag;

		public bool Has => Target != Entity.Null;

		// hard-coded
		public bool UseVariantSpawn => 
			ActionFlag == ActionFlag.Store &&
			Utilities.HasItemFlag(ItemFlags, ItemFlag.RawFood) && Utilities.HasItemFlag(UsedItemFlags, ItemFlag.Trash | ItemFlag.Contaminated);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionData(Entity target, ActionFlag actionFlag, ItemFlag itemFlags, ItemFlag usedItemFlags, float2 position, float range, float time, int cost, bool multiusableFlag = true)
		{
			Target = target;
			ActionFlag = actionFlag;
			ItemFlags = itemFlags;
			UsedItemFlags = usedItemFlags;
			Position = position;
			Range = range;
			Time = time;
			Cost = cost;

			// set to true by default to avoid unecessary action processing
			// (= instant actions do not need multiusage logic)
			MultiusableFlag = multiusableFlag;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasActionFlag(ActionFlag inFlag)
		{
			return Utilities.HasActionFlag(ActionFlag, inFlag);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool HasItemFlag(ItemFlag inFlag)
		{
			return Utilities.HasItemFlag(ItemFlags, inFlag);
		}
	}

	public struct ActionController : IComponentData
	{
		public ActionData Action;
		public float Timer;
		public bool IsResolving;
		public bool ShouldStopFlag;
		public bool ShouldDropFlag;

		public bool HasTarget => Action.Has && !ShouldStopFlag;
		public bool ShouldStop => Timer >= Action.Time || ShouldStopFlag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Start()
		{
			Timer = 0f;
			IsResolving = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Stop(bool shouldDropItem)
		{
			ShouldDropFlag = shouldDropItem;

			if (IsResolving)
			{
				// schedule stopping
				ShouldStopFlag = true;
			}
			else
			{
				// clear target
				Action.Target = Entity.Null;
				Action.ActionFlag = 0;
				IsResolving = false;
				ShouldStopFlag = false;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ShouldResolve(int credits)
		{
			return Timer >= Action.Time && (Action.Cost <= 0f || Action.Cost <= credits) && !ShouldStopFlag;
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
		public Entity CurrentUser; // always Entity.Null if cannot be multiused
		public bool Changed;
		public bool WorkingStationFlag;
		public bool CanBeMultiused;

		public bool CanBeUsed => CanBeMultiused || CurrentUser == Entity.Null;

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