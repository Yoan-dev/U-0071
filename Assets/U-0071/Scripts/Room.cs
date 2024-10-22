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
		public bool CanBeUsed => Interactable.CanBeUsed;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionData ToActionData(ActionFlag selectedActionFlag, ItemFlag itemFlags, ItemFlag usedItemFlags)
		{
			return new ActionData(Entity, selectedActionFlag, itemFlags, usedItemFlags, Position, Range, Const.GetActionTime(selectedActionFlag, Time), Cost, Interactable.CanBeMultiused);
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
				EvaluateActionFlag(ActionFlag.Contaminate, current, actionFilter, carriedFlag, out selected, false, canProcessTrash, canTeleportAll) ||
				EvaluateActionFlag(ActionFlag.Store, current, actionFilter, carriedFlag, out selected, false, canProcessTrash, canTeleportAll) ||
				EvaluateActionFlag(ActionFlag.Destroy, current, actionFilter, canDestroyAll ? ItemFlag.All : carriedFlag, out selected) ||
				EvaluateActionFlag(ActionFlag.Collect, current, actionFilter, carriedFlag, out selected) ||
				EvaluateActionFlag(ActionFlag.Search, current, actionFilter, carriedFlag, out selected) ||
				EvaluateActionFlag(ActionFlag.Pick, current, actionFilter, carriedFlag, out selected, pickHavePriority) ||
				EvaluateActionFlag(ActionFlag.Push, current, actionFilter, carriedFlag, out selected) ||
				EvaluateActionFlag(ActionFlag.Administrate, current, actionFilter, carriedFlag, out selected);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateActionFlag(ActionFlag checkedType, ActionFlag current, ActionFlag filter, ItemFlag itemFlag, out ActionFlag selected, bool hasPriority = false, bool canProcessContaminated = false, bool canTeleportAll = false)
		{
			selected = 
				(hasPriority || checkedType >= current) && 
				(ActionFlags & filter & checkedType) != 0 && 
				EvaluateItemAction(checkedType, itemFlag, canProcessContaminated, canTeleportAll) &&
				EvaluateContaminateAction(checkedType, itemFlag) ? checkedType : 0;
			
			return selected != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateItemAction(ActionFlag checkedType, ItemFlag itemFlag, bool canProcessContaminated, bool canTeleportAll)
		{
			return
				checkedType == ActionFlag.Contaminate || // evaluate later
				!Utilities.RequireItem(checkedType) ||
				HasItemFlag(itemFlag) ||
				HasActionFlag(ActionFlag.Teleport) && canTeleportAll ||
				(canProcessContaminated && HasItemFlag(ItemFlag.RawFood) && Utilities.HasItemFlag(itemFlag, ItemFlag.Contaminated));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateContaminateAction(ActionFlag checkedType, ItemFlag itemFlag)
		{
			// we need either raw food or a contaminated item
			// and we need a device that contaminate if our item is not (and opposite)
			return
				checkedType != ActionFlag.Contaminate ||
				(Utilities.HasItemFlag(itemFlag, ItemFlag.RawFood) || Utilities.HasItemFlag(itemFlag, ItemFlag.Contaminated)) &&
				HasItemFlag(ItemFlag.Contaminated) == Utilities.HasItemFlag(itemFlag, ItemFlag.Contaminated);
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

	public struct WorkInfoComponent : IComponentData
	{
		public int LevelOneOpportunityCount;
		public int LevelTwoOpportunityCount;
		public int LevelThreeOpportunityCount;
		public int AdminOpportunityCount;

		// TODO: buffer that auto-fills after world init
		public Entity Room1;
		public Entity Room2;

		// for debug purposes
		public bool ShouldLevelOneStop;
		public bool ShouldLevelTwoStop;
		public bool ShouldLevelThreeStop;
		public bool ShouldAdminStop;

		public bool ShouldStopHere(AreaAuthorization authorization)
		{
			return authorization switch
			{
				AreaAuthorization.LevelOne => LevelOneOpportunityCount > 0,
				AreaAuthorization.LevelTwo => LevelTwoOpportunityCount > 0,
				AreaAuthorization.LevelThree => LevelThreeOpportunityCount > 0,
				AreaAuthorization.Admin => AdminOpportunityCount > 0,
				_ => false,
			};
		}

		public void ProcessDebug()
		{
			ShouldLevelOneStop = ShouldStopHere(AreaAuthorization.LevelOne);
			ShouldLevelTwoStop = ShouldStopHere(AreaAuthorization.LevelTwo);
			ShouldLevelThreeStop = ShouldStopHere(AreaAuthorization.LevelThree);
			ShouldAdminStop = ShouldStopHere(AreaAuthorization.Admin);
		}
	}

	public struct CorridorOverrideComponent : IComponentData
	{
		public float2 Position;
		public int2 Dimensions;
	}
	
	public struct RoomInitTag : IComponentData, IEnableableComponent { }
}