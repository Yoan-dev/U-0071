using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	public struct ActionInfo
	{
		public ActionData Data;
		public FixedString32Bytes ActionName;
		public FixedString32Bytes TargetName;
		public FixedString32Bytes DeviceName;
		public KeyCode Key;
		public bool IsPressed;

		public int Cost => Data.Cost;
		public ActionFlag Type => Data.ActionFlag;
		public bool Has => Data.HasTarget;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset()
		{
			Data.Target = Entity.Null;
			Data.ActionFlag = 0;
		}
	}

	public struct PlayerController : IComponentData
	{
		public float2 MoveInput;
		public ActionInfo PrimaryAction;
		public ActionInfo SecondaryAction;
		public float ActionTimer;
		public AreaAuthorization CachedDoorAuthorization;
		public bool Locked;

		public bool HasPrimaryAction => PrimaryAction.Has;
		public bool HasSecondaryAction => SecondaryAction.Has;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetPrimaryAction(in ActionData data, in ComponentLookup<NameComponent> nameLookup, in ComponentLookup<ActionNameComponent> actionNameLookup, Entity usedItem)
		{
			SetAction(ref PrimaryAction, in data, in nameLookup, in actionNameLookup, usedItem);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetSecondaryAction(in ActionData data, in ComponentLookup<NameComponent> nameLookup, in ComponentLookup<ActionNameComponent> actionNameLookup, Entity usedItem)
		{
			SetAction(ref SecondaryAction, in data, in nameLookup, in actionNameLookup, usedItem);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SetAction(ref ActionInfo action, in ActionData data, in ComponentLookup<NameComponent> nameLookup, in ComponentLookup<ActionNameComponent> actionNameLookup, Entity carried)
		{
			bool hasItem = carried != Entity.Null;
			bool isContaminatingDevice = hasItem && data.ActionFlag == ActionFlag.Contaminate && Utilities.HasItemFlag(data.UsedItemFlags, ItemFlag.Contaminated);

			action.Data = data;
			action.ActionName =
				data.ActionFlag != ActionFlag.Contaminate && actionNameLookup.HasComponent(data.Target) ? actionNameLookup[data.Target].Value :
				new FixedString32Bytes();
			action.TargetName =
				isContaminatingDevice ? nameLookup[data.Target].Value :
				data.HasActionFlag(ActionFlag.Pick | ActionFlag.Search | ActionFlag.Eat | ActionFlag.Drop) ? nameLookup[data.Target].Value :
				Utilities.RequireItem(data.ActionFlag) && hasItem ? nameLookup[carried].Value :
				new FixedString32Bytes();
			action.DeviceName =
				isContaminatingDevice ? nameLookup[carried].Value :
				!data.HasActionFlag(ActionFlag.Pick | ActionFlag.Drop | ActionFlag.Eat | ActionFlag.Search) ? nameLookup[data.Target].Value :
				new FixedString32Bytes();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ShouldStartAction(in ActionInfo action, int credits)
		{
			return action.IsPressed && action.Has && (action.Data.Cost <= 0 || action.Data.Cost <= credits);
		}
	}

	public struct CameraComponent : IComponentData { }
}