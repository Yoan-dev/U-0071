using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct RoomComponent : IComponentData
	{
		public int2 Dimensions;
		public AreaAuthorization Area;
		public int Capacity; // amount of possible workers
		public int Population; // current amount of units in room
		public Entity FiredWorker; // should leave (workplace too crowded)
		public bool IsWanderPath;
	}

	[InternalBufferCapacity(32)]
	public struct RoomElementBufferElement : IBufferElementData
	{
		public Entity Entity;
		public float2 Position;
		public InteractableComponent Interactable; // cached

		public ActionFlag ActionFlags => Interactable.ActionFlags;
		public ItemFlag ItemFlags => Interactable.ItemFlags;
		public float Range => Interactable.Range;
		public float Time => Interactable.Time;
		public int Cost => Interactable.Cost;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionData ToActionData(ActionFlag selectedActionFlag, ItemFlag itemFlags, ItemFlag usedItemFlags)
		{
			return new ActionData(Entity, selectedActionFlag, itemFlags, usedItemFlags, Position, Range, Const.GetActionTime(selectedActionFlag, Time), Cost);
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
		public bool Evaluate(ActionFlag current, ActionFlag actionFilter, ItemFlag carriedFlag, out ActionFlag selected, bool canDestroyAll = false, bool pickHavePriority = false, bool canProcessTrash = false, bool canTeleportAll = false)
		{
			// retrieve eligible action in priority order
			return
				EvaluateActionFlag(ActionFlag.Store, current, actionFilter, carriedFlag, out selected, false, canProcessTrash, canTeleportAll) ||
				EvaluateActionFlag(ActionFlag.Destroy, current, actionFilter, canDestroyAll ? ItemFlag.All : carriedFlag, out selected) ||
				EvaluateActionFlag(ActionFlag.Collect, current, actionFilter, carriedFlag, out selected) ||
				EvaluateActionFlag(ActionFlag.Search, current, actionFilter, carriedFlag, out selected) ||
				EvaluateActionFlag(ActionFlag.Pick, current, actionFilter, carriedFlag, out selected, pickHavePriority) ||
				EvaluateActionFlag(ActionFlag.Push, current, actionFilter, carriedFlag, out selected);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateActionFlag(ActionFlag checkedType, ActionFlag current, ActionFlag filter, ItemFlag itemFlag, out ActionFlag selected, bool hasPriority = false, bool canProcessTrash = false, bool canTeleportAll = false)
		{
			selected = 
				(hasPriority || checkedType >= current) && 
				(ActionFlags & filter & checkedType) != 0 && 
				(!Utilities.RequireItem(checkedType) || HasItemFlag(itemFlag) || HasActionFlag(ActionFlag.Teleport) && canTeleportAll || (canProcessTrash && HasItemFlag(ItemFlag.RawFood) && Utilities.HasItemFlag(itemFlag, ItemFlag.Trash))) ? checkedType : 0;
			return selected != 0;
		}

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

	public struct CorridorOverrideComponent : IComponentData
	{
		public float2 Position;
		public int2 Dimensions;
	}
	
	public struct RoomInitTag : IComponentData, IEnableableComponent { }
}