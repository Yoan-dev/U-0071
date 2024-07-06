using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct NameComponent : IComponentData
	{
		public FixedString32Bytes Value;
	}

	public struct PositionComponent : IComponentData
	{
		public float2 Value;

		public float x => Value.x;
		public float y => Value.y;
	}
}