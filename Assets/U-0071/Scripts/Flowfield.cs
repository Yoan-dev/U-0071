using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct Flowfield : IComponentData, IDisposable
	{
		public NativeArray<float2> FoodLevelZero;
		public NativeArray<float2> DestroyLevelZero;
		public int2 Dimensions;

		public Flowfield(int2 dimensions)
		{
			Dimensions = dimensions;
			FoodLevelZero = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
			DestroyLevelZero = new NativeArray<float2>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			FoodLevelZero.Dispose();
			DestroyLevelZero.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetIndex(float2 position)
		{
			return (int)(position.x + Dimensions.x / 2) + (int)(position.y + Dimensions.y / 2) * Dimensions.x;
		}
	}
}