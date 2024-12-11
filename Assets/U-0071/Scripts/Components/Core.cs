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
		public float2 LastPosition;
		public float BaseZOffset; // transform adjustment
		public float BaseYOffset;
		public float CurrentYOffset; // for sorting

		public float x => Value.x;
		public float y => Value.y;
		public bool HasMoved => !Value.Equals(LastPosition);
		public bool HasSlightlyMoved => math.abs(Value.x - LastPosition.x) > Const.Small || math.abs(Value.y - LastPosition.y) > Const.Small;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsInRange(float2 position, float range)
		{
			return math.lengthsq(Value - position) <= math.pow(range, 2f);
		}
	}
}