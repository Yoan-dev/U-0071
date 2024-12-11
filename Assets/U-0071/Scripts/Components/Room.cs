using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct RoomComponent : IComponentData
	{
		public int2 Dimensions;
		public AreaAuthorization Authorization; // room access level
		public int Capacity; // amount of possible workers
		public int Population; // current amount of units in room
		public Entity FiredWorker; // should leave (workplace too crowded)
		public bool IsWanderPath; // used for AI navigation (hallways)
	}

	[InternalBufferCapacity(32)]
	public struct RoomElementBufferElement : IBufferElementData
	{
		// rooms cache their interactables (units, items, devices) for easy access by enquiring processes
		public Entity Entity;
		public float2 Position;
		public InteractableComponent Interactable;

		public ActionFlag ActionFlags => Interactable.ActionFlags;
		public ItemFlag ItemFlags => Interactable.ItemFlags;
		public float Range => Interactable.Range;
		public float Time => Interactable.Time;
		public int Cost => Interactable.Cost;
		public bool CanBeUsed => Interactable.CanBeUsed;

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
		public ActionData ToActionData(ActionFlag selectedActionFlag, ItemFlag itemFlags, ItemFlag usedItemFlags)
		{
			return new ActionData(Entity, selectedActionFlag, itemFlags, usedItemFlags, Position, Range, Const.GetActionTime(selectedActionFlag, Time), Cost, Interactable.CanBeMultiused);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Evaluate(ActionFlag current, ActionFlag actionFilter, ItemFlag carriedFlag, out ActionFlag selectedAction, bool canDestroyAll = false, bool pickHavePriority = false, bool canProcessTrash = false, bool canTeleportAll = false)
		{
			// retrieve eligible action in priority order
			return
				EvaluateActionFlag(ActionFlag.Contaminate, current, actionFilter, carriedFlag, out selectedAction, false, canProcessTrash, canTeleportAll) ||
				EvaluateActionFlag(ActionFlag.Store, current, actionFilter, carriedFlag, out selectedAction, false, canProcessTrash, canTeleportAll) ||
				EvaluateActionFlag(ActionFlag.Destroy, current, actionFilter, canDestroyAll ? ItemFlag.All : carriedFlag, out selectedAction) ||
				EvaluateActionFlag(ActionFlag.Collect, current, actionFilter, carriedFlag, out selectedAction) ||
				EvaluateActionFlag(ActionFlag.Search, current, actionFilter, carriedFlag, out selectedAction) ||
				EvaluateActionFlag(ActionFlag.Pick, current, actionFilter, carriedFlag, out selectedAction, pickHavePriority) ||
				EvaluateActionFlag(ActionFlag.Push, current, actionFilter, carriedFlag, out selectedAction) ||
				EvaluateActionFlag(ActionFlag.Administrate, current, actionFilter, carriedFlag, out selectedAction);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateActionFlag(ActionFlag checkedType, ActionFlag current, ActionFlag filter, ItemFlag itemFlag, out ActionFlag selectedAction, bool hasPriority = false, bool canProcessContaminated = false, bool canTeleportAll = false)
		{
			// verify action validity
			bool isValid =
				(hasPriority || checkedType >= current) &&
				(ActionFlags & filter & checkedType) != 0 &&
				EvaluateItemAction(checkedType, itemFlag, canProcessContaminated, canTeleportAll) &&
				EvaluateContaminateAction(checkedType, itemFlag);

			selectedAction = isValid ? checkedType : 0;
			
			return isValid;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateItemAction(ActionFlag checkedType, ItemFlag itemFlag, bool canProcessContaminated, bool canTeleportAll)
		{
			return
				checkedType == ActionFlag.Contaminate || // evaluate later
				!Utilities.RequireItem(checkedType) ||
				HasItemFlag(itemFlag) ||
				HasActionFlag(ActionFlag.TeleportItem) && canTeleportAll ||
				(canProcessContaminated && HasItemFlag(ItemFlag.RawFood) && Utilities.HasItemFlag(itemFlag, ItemFlag.Contaminated));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool EvaluateContaminateAction(ActionFlag checkedType, ItemFlag itemFlag)
		{
			// contamination actions are either contaminating a hold item (raw food)
			// or contaminating a target with an already contaminated item in hand

			bool isContaminationEligible = Utilities.HasItemFlag(itemFlag, ItemFlag.RawFood) || Utilities.HasItemFlag(itemFlag, ItemFlag.Contaminated);

			return
				checkedType != ActionFlag.Contaminate ||
				isContaminationEligible && HasItemFlag(ItemFlag.Contaminated) == Utilities.HasItemFlag(itemFlag, ItemFlag.Contaminated);
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
		// component that tracks nearby work opportunities (hallways)
		public int LevelOneOpportunityCount;
		public int LevelTwoOpportunityCount;
		public int LevelThreeOpportunityCount;
		public int AdminOpportunityCount;

		// target rooms (workplaces, hard-coded to two)
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
		// allow to consider part of a room as a hallway (for AI navigation)
		public float2 Position;
		public int2 Dimensions;
	}
	
	public struct RoomInitTag : IComponentData, IEnableableComponent { }
}