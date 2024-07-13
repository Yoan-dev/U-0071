using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct MovementComponent : IComponentData
	{
		public float2 Input;
		public float Speed;
		public bool IsRunning;
		public bool FreeMovement;
	}
}