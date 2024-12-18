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
			Entity entity,
			ref ActionController controller,
			in Orientation orientation,
			in PositionComponent position,
			in PartitionInfoComponent partitionInfo,
			EnabledRefRW<IsActing> isActing,
			EnabledRefRO<DeathComponent> death,
			EnabledRefRO<PushedComponent> pushed,
			in ComponentLookup<InteractableComponent> interactableLookup,
			in ComponentLookup<PickableComponent> pickableLookup,
			in CarryComponent carry)
		{
			// returns "should stop" to AI/Player controller jobs

			if (death.ValueRO || pushed.ValueRO)
			{
				controller.Stop(carry.HasItem, false);
				return true;
			}

			if (!controller.IsResolving && controller.ShouldSpreadDiseaseFlag)
			{
				QueueSpreadDiseaseAction(ref controller, in orientation, in position, in partitionInfo, isActing);
				return true;
			}

			if (!carry.HasItem && controller.ShouldDropFlag) controller.ShouldDropFlag = false;

			if (controller.HasTarget && (
				!interactableLookup.TryGetComponent(controller.Action.Target, out InteractableComponent interactable) ||
				(controller.IsResolving && !position.IsInRange(controller.Action.Position, interactable.Range)) ||
				interactable.CurrentUser != Entity.Null && interactable.CurrentUser != entity ||
				!controller.IsResolving && interactable.HasActionFlag(ActionFlag.Pick) && pickableLookup.IsComponentEnabled(controller.Action.Target) ||
				!interactable.HasActionFlag(controller.Action.ActionFlag)))
			{
				// target is being solo-used or has been destroyed/picked/disabled
				controller.Stop(false, false);
				return true;
			}

			// cannot act if not in partition or already acting
			if (partitionInfo.CurrentRoom == Entity.Null || controller.IsResolving)
			{
				return true;
			}

			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void QueueDropAction(
			ref ActionController controller,
			in Orientation orientation,
			in PositionComponent position,
			in CarryComponent carry,
			in PartitionInfoComponent partitionInfo,
			EnabledRefRW<IsActing> isActing)
		{
			controller.Action = new ActionData(carry.Picked, ActionFlag.Drop, 0, carry.Flags, GetDropPosition(position.Value, orientation.Value, partitionInfo.ClosestEdgeX), 0f, 0f, 0);
			controller.Start();
			isActing.ValueRW = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void QueueSpreadDiseaseAction(
			ref ActionController controller,
			in Orientation orientation,
			in PositionComponent position,
			in PartitionInfoComponent partitionInfo,
			EnabledRefRW<IsActing> isActing)
		{
			controller.ShouldSpreadDiseaseFlag = false;
			controller.Action = new ActionData(Entity.Null, ActionFlag.SpreadDisease, 0, 0, GetDropPosition(position.Value, orientation.Value, partitionInfo.ClosestEdgeX), 0f, Const.VomitResolveTime, 0);
			controller.Start();
			isActing.ValueRW = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float2 GetDropPosition(float2 position, float orientation, float closestEdgePosition)
		{
			float x = position.x + Const.DropOffsetX * orientation;
			bool isOutOfRoom = 
				orientation > 0 && position.x <= closestEdgePosition && x >= closestEdgePosition || 
				orientation < 0 && position.x >= closestEdgePosition && x <= closestEdgePosition;

			return isOutOfRoom ? position : new float2(x, position.y + Const.DropOffsetY);
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
			// return by inverse-priority
			return
				HasAuthorization(areaFlags, AreaAuthorization.LevelOne) ? AreaAuthorization.LevelOne :
				HasAuthorization(areaFlags, AreaAuthorization.LevelTwo) ? AreaAuthorization.LevelTwo :
				HasAuthorization(areaFlags, AreaAuthorization.LevelThree) ? AreaAuthorization.LevelThree :
				HasAuthorization(areaFlags, AreaAuthorization.Red) ? AreaAuthorization.Red :
				HasAuthorization(areaFlags, AreaAuthorization.Blue) ? AreaAuthorization.Blue :
				HasAuthorization(areaFlags, AreaAuthorization.Yellow) ? AreaAuthorization.Yellow :
				HasAuthorization(areaFlags, AreaAuthorization.Admin) ? AreaAuthorization.Admin : 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetCurrentAwareness(bool isSick, AIGoal currentGoal)
		{
			return 1f + (isSick ? Const.SicknessAwarenessModifier : 0f) + (currentGoal == AIGoal.Flee ? Const.PanicAwarenessModifier : 0f);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool RequireItem(ActionFlag actionFlag)
		{
			return actionFlag == ActionFlag.Store || actionFlag == ActionFlag.Destroy || actionFlag == ActionFlag.TeleportItem || actionFlag == ActionFlag.Contaminate;
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
		public static float SmoothStep(float value, float start, float end)
		{
			if (value < start) return 0f;
			if (value >= end) return 1f;
			
			value = (value - start) / (end - start);
			return value * value * (3 - 2 * value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float4 ToFloat4(this Color color)
		{
			return new float4(color.r, color.g, color.b, color.a);
		}

		public static string GetAuthorizationText(AreaAuthorization authorization)
		{
			return authorization switch
			{
				AreaAuthorization.LevelOne => "LVL1",
				AreaAuthorization.LevelTwo => "LVL2",
				AreaAuthorization.LevelThree => "LVL3",
				AreaAuthorization.Red => "RED",
				AreaAuthorization.Blue => "BLUE",
				AreaAuthorization.Yellow => "YELLOW",
				AreaAuthorization.Admin => "ADMIN",
				_ => "ERROR",
			};
		}
	}
}