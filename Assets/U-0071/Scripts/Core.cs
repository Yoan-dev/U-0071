using System.Runtime.CompilerServices;
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
		public bool MovedFlag;

		public float x => Value.x;
		public float y => Value.y;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(float2 value)
		{
			Value = value;
			MovedFlag = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(float2 value)
		{
			Value += value;
			MovedFlag = MovedFlag || !value.Equals(float2.zero);
		}
	}
}