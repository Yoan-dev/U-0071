using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct MovementComponent : IComponentData
	{
		public float Speed;
		public float2 Input;
	}
}