using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	public struct ActionInput
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
		public ActionTarget Primary;
		public ActionTarget Secondary;
		public ActionInput PrimaryInfo;
		public ActionInput SecondaryInfo;

		public bool HasPrimaryAction => Primary.Has;
		public bool HasSecondaryAction => Secondary.Has;
	}

	public struct CameraComponent : IComponentData { }
}