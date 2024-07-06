using Unity.Entities;

namespace U0071
{
	public enum ActionType
	{
		Pick = 0,
		Drop,
	}

	public struct CarryComponent : IComponentData
	{
		public Entity Carried;
	}

	public struct Pickable : IComponentData { }
}