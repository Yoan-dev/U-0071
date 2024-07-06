using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public static class Utilities
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetClosestRoomElement(in DynamicBuffer<RoomElementBufferElement> elements, float2 position, Entity self, ActionType filter, out RoomElementBufferElement element)
		{
			element = new RoomElementBufferElement();

			float minMagn = float.MaxValue;
			using (var enumerator = elements.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.Element != self && IsActionType(enumerator.Current.ActionType, filter))
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
			return element.Element != Entity.Null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsActionType(ActionType type, ActionType check)
		{
			return (type & check) == check;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static EntityManager GetEntityManager()
		{
			return World.DefaultGameObjectInjectionWorld.Unmanaged.EntityManager;
		}
	}
}