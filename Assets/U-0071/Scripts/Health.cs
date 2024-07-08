using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct HungerComponent : IComponentData
	{
		public float Value;
	}

	public struct PushedComponent : IComponentData, IEnableableComponent
	{
		public float2 Direction;
		public float Timer;
	}

	public struct IsSickTag : IComponentData, IEnableableComponent { }

	public struct IsDeadTag : IComponentData, IEnableableComponent { }
}