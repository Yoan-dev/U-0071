using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	public struct InteractionInput
	{
		public FixedString32Bytes Name;
		public ActionType Type;
		public KeyCode Key;
		public bool IsPressed;
	}

	public struct PlayerController : IComponentData
	{
		public float2 MoveInput;
		public float2 LookInput;
		public InteractionInput FirstInteraction;
		public InteractionInput SecondInteraction;
	}

	public struct CameraComponent : IComponentData { }
}