using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	public static class Utilities
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T GetSystem<T>(ref SystemState state) where T : unmanaged, ISystem
		{
			return state.WorldUnmanaged.GetUnsafeSystemRef<T>(World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<T>());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetClosestRoomElement(in DynamicBuffer<RoomElementBufferElement> elements, float2 position, Entity self, ActionType filter, out RoomElementBufferElement element)
		{
			element = new RoomElementBufferElement();

			float minMagn = float.MaxValue;
			using (var enumerator = elements.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.Entity != self && HasActionType(enumerator.Current.ActionFlags, filter))
					{
						float magn = math.lengthsq(position - enumerator.Current.Position);
						if (magn < minMagn)
						{
							minMagn = magn;
							element = enumerator.Current;
						}
					}
				}
			}
			return element.Entity != Entity.Null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsActionType(ActionType type, ActionType check)
		{
			return (type & check) == check;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasActionType(ActionType type, ActionType check)
		{
			return (type & check) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static EntityManager GetEntityManager()
		{
			return World.DefaultGameObjectInjectionWorld.Unmanaged.EntityManager;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float4 ToFloat4(this Color color)
		{
			return new float4(color.r, color.g, color.b, color.a);
		}
	}
}