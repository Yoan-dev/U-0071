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
		public ActionType Type;
		public int Cost;
		public bool IsPressed;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset()
		{
			Data.Target = Entity.Null;
			Type = 0;
		}
	}

	public struct PlayerController : IComponentData
	{
		public float2 MoveInput;
		public float2 LookInput;
		public ActionInfo PrimaryAction;
		public ActionInfo SecondaryAction;

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
			action.Type = data.Type;
			action.Name = 
				data.Type != ActionType.Pick && data.Type != ActionType.Drop ? nameLookup[data.Target].Value :
				new FixedString32Bytes();
			action.SecondaryName =
				data.Type == ActionType.Pick ? nameLookup[data.Target].Value :
				usedItem != Entity.Null ? nameLookup[usedItem].Value :
				new FixedString32Bytes();
			action.Cost = data.Cost;
		}
	}

	public struct CameraComponent : IComponentData { }
}