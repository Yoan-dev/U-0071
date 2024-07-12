using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

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
			Entity entity,
			ref ActionController controller,
			ref Orientation orientation,
			in PositionComponent position,
			in CarryComponent carry,
			in PartitionComponent partition,
			EnabledRefRW<IsActing> isActing,
			EnabledRefRO<DeathComponent> death,
			EnabledRefRO<PushedComponent> pushed,
			in ComponentLookup<InteractableComponent> interactableLookup,
			in ComponentLookup<PickableComponent> pickableLookup)
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

			if (controller.HasTarget && (
				!interactableLookup.TryGetComponent(controller.Action.Target, out InteractableComponent interactable) ||
				interactable.CurrentUser != Entity.Null && interactable.CurrentUser != entity ||
				interactable.HasActionFlag(ActionFlag.Pick) && pickableLookup.IsComponentEnabled(controller.Action.Target) ||
				!interactable.HasActionFlag(controller.Action.ActionFlag)))
			{
				// target is being solo-used or has been destroyed/picked/disabled
				controller.Stop();
				return false;
			}

			// cannot act if not in partition or already acting
			if (partition.CurrentRoom == Entity.Null || controller.IsResolving)
			{
				return true;
			}

			return false;
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
		public static bool HasAuthorization(AreaAuthorization flag, AreaAuthorization check)
		{
			return (flag & check) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool CompareAuthorization(AreaAuthorization area1, AreaAuthorization area2)
		{
			// admin areas should exclude each other but this check will be enough
			// (level design will have admin areas as dead ends)
			return area1 >= area2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AreaAuthorization GetLowestAuthorization(AreaAuthorization areaFlags)
		{
			// return by inverse-priority (meh bitwise magic to use ?)
			return
				HasAuthorization(areaFlags, AreaAuthorization.LevelOne) ? AreaAuthorization.LevelOne :
				HasAuthorization(areaFlags, AreaAuthorization.LevelTwo) ? AreaAuthorization.LevelTwo :
				HasAuthorization(areaFlags, AreaAuthorization.LevelThree) ? AreaAuthorization.LevelThree :
				HasAuthorization(areaFlags, AreaAuthorization.Red) ? AreaAuthorization.Red :
				HasAuthorization(areaFlags, AreaAuthorization.Blue) ? AreaAuthorization.Blue :
				HasAuthorization(areaFlags, AreaAuthorization.Yellow) ? AreaAuthorization.Yellow : 0;
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
		public static bool IsInBounds(float2 point, in BoundsComponent bounds)
		{
			return
				point.x >= bounds.Min.x && point.y >= bounds.Min.y &&
				point.x <= bounds.Max.x && point.y <= bounds.Max.y;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsDirectionTowards(float2 from, float2 direction, float2 to, float angle)
		{
			float2 toDirection = math.normalizesafe(to - from);
			float dotProduct = math.dot(direction, toDirection);
			float threshold = math.cos(angle / 2);
			return dotProduct > threshold;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float EaseOutCubic(float value, float strength)
		{
			return 1f - math.pow(1f - value, strength);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float4 ToFloat4(this Color color)
		{
			return new float4(color.r, color.g, color.b, color.a);
		}
	}
}