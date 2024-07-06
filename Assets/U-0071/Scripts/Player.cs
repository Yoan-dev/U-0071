using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct PlayerController : IComponentData
	{
		public float2 MoveInput;
		public float2 LookInput;
	}

	public struct CameraComponent : IComponentData { }
}