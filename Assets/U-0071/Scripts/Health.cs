using Unity.Entities;

namespace U0071
{
	public struct HungerComponent : IComponentData
	{
		public float Value;
	}

	public struct IsSickTag : IComponentData, IEnableableComponent { }

	public struct IsDeadTag : IComponentData, IEnableableComponent { }
}