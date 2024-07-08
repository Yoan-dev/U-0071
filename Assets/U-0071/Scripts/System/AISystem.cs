using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static UnityEngine.GraphicsBuffer;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private EntityQuery _query;
		private float _timer;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<RoomPartition>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<ActionController, Orientation>()
				.WithAll<AITag, PositionComponent, PartitionComponent, CreditsComponent>()
				.WithPresent<PickComponent>()
				.WithPresentRW<IsActing>()
				.Build();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_positionLookup = state.GetComponentLookup<PositionComponent>(true);
			_pickableLookup = state.GetComponentLookup<PickableComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// TODO: batching
			_timer += SystemAPI.Time.DeltaTime;
			if (_timer >= Const.AITick)
			{
				_timer -= Const.AITick;

				_roomElementLookup.Update(ref state);
				_positionLookup.Update(ref state);
				_pickableLookup.Update(ref state);

				state.Dependency = new AIActionJob
				{
					RoomElementBufferLookup = _roomElementLookup,
					PositionLookup = _positionLookup,
					PickableLookup = _pickableLookup,
				}.ScheduleParallel(_query, state.Dependency);

				state.Dependency = new AIMovementJob().ScheduleParallel(state.Dependency);
			}
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct AIActionJob : IJobEntity
		{
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<PositionComponent> PositionLookup;
			[ReadOnly]
			public ComponentLookup<PickableComponent> PickableLookup;

			public void Execute(
				Entity entity,
				ref ActionController controller,
				ref Orientation orientation,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition,
				in CreditsComponent credits,
				EnabledRefRW<IsActing> isActing)
			{
				// evaluate if action target is still relevant (ex: spawner capacity 0)
				// TODO

				// cannot act if not in partition or already acting
				if (partition.CurrentRoom == Entity.Null || controller.IsResolving) return;

				// TODO: consider picked interactable action

				// make sure target is still avaialable (not destroyed or picked)
				if (controller.HasTarget &&
					(!PickableLookup.HasComponent(controller.Action.Target) || !PickableLookup.IsComponentEnabled(controller.Action.Target)) &&
					PositionLookup.HasComponent(controller.Action.Target))
				{
					// note: we could assume that the target is not moving
					// (use cached value)
					controller.Action.Position = PositionLookup[controller.Action.Target].Value;

					if (position.IsInRange(controller.Action.Position, controller.Action.Range))
					{
						// start interacting
						isActing.ValueRW = true;
						controller.Start();
						orientation.Update(controller.Action.Position.x - position.Value.x);
					}
				}
				else
				{
					// reset if picked / destroyed
					controller.Action.Target = Entity.Null;

					// retrieve relevant action types
					ActionType filter = ActionType.AllActions;

					if (pick.Picked != Entity.Null)
					{
						filter &= ~ActionType.Pick;
					}
					else
					{
						filter &= ~ActionType.Trash;
					}

					// look for target
					if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, filter, credits.Value, out RoomElementBufferElement target))
					{
						// consider which action to do in priority
						ActionType actionType = 0;
						if (CanExecuteAction(ActionType.Collect, filter, in target))
						{
							actionType = ActionType.Collect;
						}
						else if (CanExecuteAction(ActionType.Pick, filter, in target))
						{
							actionType = ActionType.Pick;
						}
						else if (CanExecuteAction(ActionType.Trash, filter, in target))
						{
							actionType = ActionType.Trash;
						}

						// queue action or track target
						if (actionType != 0)
						{
							controller.Action = target.ToActionData(actionType);

							if (position.IsInRange(target.Position, target.Range))
							{
								// start interacting
								isActing.ValueRW = true;
								controller.Start();
								orientation.Update(controller.Action.Position.x - position.Value.x);
							}
						}
					}
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private bool CanExecuteAction(ActionType type, ActionType filter, in RoomElementBufferElement target)
			{
				// note: credits vs cost already checked during query
				return Utilities.HasActionType(filter, type) && Utilities.HasActionType(target.ActionFlags, type);
			}
		}

		[BurstCompile]
		[WithAll(typeof(AITag))]
		public partial struct AIMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PositionComponent position, in ActionController controller)
			{
				if (!controller.IsResolving && controller.HasTarget)
				{
					float2 direction = controller.Action.Position - position.Value;
					movement.Input = math.lengthsq(direction) > math.pow(controller.Action.Range, 2f) ? math.normalize(direction) : float2.zero;
				}
				else
				{
					movement.Input = float2.zero;
				}
			}
		}

	}
}