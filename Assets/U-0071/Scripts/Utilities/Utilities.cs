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
			in PickComponent pick,
			in PartitionComponent partition,
			EnabledRefRW<IsActing> isActingRefRW,
			EnabledRefRO<DeathComponent> deathRefRO,
			EnabledRefRO<PushedComponent> pushedRefRO)
		{
			// returns "should stop" to AI/Player controller jobs

			if (deathRefRO.ValueRO || pushedRefRO.ValueRO)
			{
				if (pick.Picked != Entity.Null)
				{
					// drop item on death/pushed
					controller.Action = new ActionData(pick.Picked, ActionType.Drop, position.Value + Const.GetDropOffset(orientation.Value), 0f, 0f, 0);
					controller.Start();
					isActingRefRW.ValueRW = true;
				}
				return true;
			}

			// cannot act if not in partition or already acting
			return partition.CurrentRoom == Entity.Null || controller.IsResolving;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasActionType(ActionType type, ActionType check)
		{
			return (type & check) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool RequireCarriedItem(ActionType type)
		{
			return type == ActionType.Store || type == ActionType.Destroy;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool CheckStoreActionEligibility(ActionType flags, ActionType filter)
		{
			// TODO: find a fancier way
			if (HasActionType(flags, ActionType.Store))
			{
				return
					HasActionType(flags, ActionType.RefProcess) && HasActionType(filter, ActionType.RefProcess) ||
					HasActionType(flags, ActionType.RefEat) && HasActionType(filter, ActionType.RefEat) ||
					HasActionType(flags, ActionType.RefTrash) && HasActionType(filter, ActionType.RefTrash);
			}
			return true;
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