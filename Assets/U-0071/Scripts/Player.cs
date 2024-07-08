using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	public struct ActionInfo
	{
		public FixedString32Bytes Name;
		public KeyCode Key;
		public ActionType Type;
		public bool IsPressed;
	}

	public struct PlayerController : IComponentData
	{
		public float2 MoveInput;
		public float2 LookInput;
		public ActionData PrimaryTarget;
		public ActionData SecondaryTarget;
		public ActionInfo PrimaryInfo;
		public ActionInfo SecondaryInfo;

		public bool HasPrimaryAction => PrimaryTarget.Has;
		public bool HasSecondaryAction => SecondaryTarget.Has;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetPrimaryAction(in RoomElementBufferElement target, in ComponentLookup<NameComponent> nameLookup, ActionType type)
		{
			SetAction(ref PrimaryTarget, ref PrimaryInfo, target.ToActionData(type), in nameLookup, type);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetSecondaryAction(in RoomElementBufferElement target, in ComponentLookup<NameComponent> nameLookup, ActionType type)
		{
			SetAction(ref SecondaryTarget, ref SecondaryInfo, target.ToActionData(type), in nameLookup, type);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetPrimaryAction(in ActionData data, in ComponentLookup<NameComponent> nameLookup, ActionType type)
		{
			SetAction(ref PrimaryTarget, ref PrimaryInfo, in data, in nameLookup, type);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetSecondaryAction(in ActionData data, in ComponentLookup<NameComponent> nameLookup, ActionType type)
		{
			SetAction(ref SecondaryTarget, ref SecondaryInfo, in data, in nameLookup, type);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetAction(ref ActionData action, ref ActionInfo info, in ActionData data, in ComponentLookup<NameComponent> nameLookup, ActionType type)
		{
			action = data;
			info.Name = nameLookup[data.Target].Value;
			info.Type = type;
		}
	}

	public struct CameraComponent : IComponentData { }
}