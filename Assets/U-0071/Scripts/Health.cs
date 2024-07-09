using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public enum DeathContext
	{
		Hunger = 0,
		Crushed,
		Electric,
	}

	public struct HungerComponent : IComponentData
	{
		public float Value;
	}

	public struct PushedComponent : IComponentData, IEnableableComponent
	{
		public float2 Direction;
		public float Timer;
	}

	public struct DeathComponent : IComponentData, IEnableableComponent
	{
		public DeathContext Context;
		public bool IsResolved;
	}

	public struct IsSickTag : IComponentData, IEnableableComponent { }
}