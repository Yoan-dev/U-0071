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
		public FixedString32Bytes TargetName;
		public FixedString32Bytes DeviceName;
		public KeyCode Key;
		public bool IsPressed;

		public int Cost => Data.Cost;
		public ActionFlag Type => Data.ActionFlag;
		public bool Has => Data.Has;

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
		public float2 LookInput;
		public ActionInfo PrimaryAction;
		public ActionInfo SecondaryAction;
		public float ActionTimer;

		public bool HasPrimaryAction => PrimaryAction.Has;
		public bool HasSecondaryAction => SecondaryAction.Has;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetPrimaryAction(in ActionData data, in ComponentLookup<NameComponent> nameLookup, Entity usedItem)
		{
			SetAction(ref PrimaryAction, in data, in nameLookup, usedItem);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetSecondaryAction(in ActionData data, in ComponentLookup<NameComponent> nameLookup, Entity usedItem)
		{
			SetAction(ref SecondaryAction, in data, in nameLookup, usedItem);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SetAction(ref ActionInfo action, in ActionData data, in ComponentLookup<NameComponent> nameLookup, Entity carried)
		{
			action.Data = data;
			action.TargetName =
				data.HasActionFlag(ActionFlag.Pick | ActionFlag.Search) ? nameLookup[data.Target].Value :
				Utilities.RequireItem(data.ActionFlag) && carried != Entity.Null ? nameLookup[carried].Value :
				new FixedString32Bytes();
			action.DeviceName = 
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