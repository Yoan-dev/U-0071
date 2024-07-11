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
		public static EntityManager GetEntityManager()
		{
			return World.DefaultGameObjectInjectionWorld.Unmanaged.EntityManager;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ProcessUnitControllerStart(
			ref ActionController controller,
			ref Orientation orientation,
			in PositionComponent position,
			in CarryComponent carry,
			in PartitionComponent partition,
			EnabledRefRW<IsActing> isActing,
			EnabledRefRO<DeathComponent> death,
			EnabledRefRO<PushedComponent> pushed)
		{
			// returns "should stop" to AI/Player controller jobs

			if (death.ValueRO || pushed.ValueRO)
			{
				if (carry.HasItem)
				{
					// drop item on death/pushed
					QueueDropAction(ref controller, ref orientation, in position, in carry, isActing);
				}
				return true;
			}

			// cannot act if not in partition or already acting
			return partition.CurrentRoom == Entity.Null || controller.IsResolving;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void QueueDropAction(
			ref ActionController controller,
			ref Orientation orientation,
			in PositionComponent position,
			in CarryComponent carry,
			EnabledRefRW<IsActing> isActing)
		{
			controller.Action = new ActionData(carry.Picked, ActionFlag.Drop, 0, carry.Flags, position.Value + Const.GetDropOffset(orientation.Value), 0f, 0f, 0);
			controller.Start();
			isActing.ValueRW = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasActionFlag(ActionFlag flag, ActionFlag check)
		{
			return (flag & check) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasItemFlag(ItemFlag flag, ItemFlag check)
		{
			return (flag & check) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAuthorization(AreaAuthorisation flag, AreaAuthorisation check)
		{
			return (flag & check) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool CompareAuthorization(AreaAuthorisation area1, AreaAuthorisation area2)
		{
			// admin areas should exclude each other but this check will be enough
			// (level design will have admin areas as dead ends)
			return area1 >= area2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool RequireItem(ActionFlag actionFlag)
		{
			return actionFlag == ActionFlag.Store || actionFlag == ActionFlag.Destroy || actionFlag == ActionFlag.Teleport;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInCircle(float2 point, float2 center, float radius)
		{
			return math.lengthsq(point - center) <= math.pow(radius, 2f);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float4 ToFloat4(this Color color)
		{
			return new float4(color.r, color.g, color.b, color.a);
		}
	}
}