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
		public FixedString32Bytes Name;
		public FixedString32Bytes SecondaryName;
		public KeyCode Key;
		public bool IsPressed;

		public int Cost => Data.Cost;
		public ActionType Type => Data.Type;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset()
		{
			Data.Target = Entity.Null;
		}
	}

	public struct PlayerController : IComponentData
	{
		public float2 MoveInput;
		public float2 LookInput;
		public ActionInfo PrimaryAction;
		public ActionInfo SecondaryAction;
		public float ActionTimer;

		public bool HasPrimaryAction => PrimaryAction.Data.Has;
		public bool HasSecondaryAction => SecondaryAction.Data.Has;

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
		private void SetAction(ref ActionInfo action, in ActionData data, in ComponentLookup<NameComponent> nameLookup, Entity usedItem)
		{
			action.Data = data;
			action.Name = 
				data.Type != ActionType.Pick && data.Type != ActionType.Drop && data.Type != ActionType.Eat ? nameLookup[data.Target].Value :
				new FixedString32Bytes();
			action.SecondaryName =
				data.Type == ActionType.Pick ? nameLookup[data.Target].Value :
				usedItem != Entity.Null ? nameLookup[usedItem].Value :
				new FixedString32Bytes();
		}
	}

	public struct CameraComponent : IComponentData { }
}