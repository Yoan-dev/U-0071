using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		public EventCollector<ActionEventBufferElement> _actionEventCollector;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private EntityQuery _query;
		private float _timer;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<ActionEventBufferElement>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<AIController>()
				.WithAll<PositionComponent, PartitionComponent>()
				.WithPresent<PickComponent>()
				.Build();

			_actionEventCollector = new EventCollector<ActionEventBufferElement>(SystemAPI.GetBufferLookup<ActionEventBufferElement>());
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_positionLookup = state.GetComponentLookup<PositionComponent>(true);
			_pickableLookup = state.GetComponentLookup<PickableComponent>(true);
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_actionEventCollector.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// TODO: batching
			_timer += SystemAPI.Time.DeltaTime;
			if (_timer >= Const.AITick)
			{
				_timer -= Const.AITick;

				_actionEventCollector.Update(ref state, _query.CalculateEntityCount());
				_roomElementLookup.Update(ref state);
				_positionLookup.Update(ref state);
				_pickableLookup.Update(ref state);

				state.Dependency = new AIActionJob
				{
					ActionEvents = _actionEventCollector.Writer,
					RoomElementBufferLookup = _roomElementLookup,
					PositionLookup = _positionLookup,
					PickableLookup = _pickableLookup,
				}.ScheduleParallel(_query, state.Dependency);

				state.Dependency = _actionEventCollector.WriteEventsToBuffer(state.Dependency);
			
				state.Dependency = new AIMovementJob().ScheduleParallel(state.Dependency);
			}
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct AIActionJob : IJobEntity
		{
			[WriteOnly]
			public NativeList<ActionEventBufferElement>.ParallelWriter ActionEvents;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<PositionComponent> PositionLookup;
			[ReadOnly]
			public ComponentLookup<PickableComponent> PickableLookup;

			public void Execute(
				Entity entity,
				ref AIController controller,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition)
			{
				// cannot act if not in partition
				if (partition.CurrentRoom == Entity.Null) return;

				// TODO: consider picked interactable action

				// make sure target is still avaialable (not destroyed or picked)
				if (controller.HasTarget &&
					(!PickableLookup.HasComponent(controller.Target.Target) || !PickableLookup.IsComponentEnabled(controller.Target.Target)) &&
					PositionLookup.HasComponent(controller.Target.Target))
				{
					// note: we could assume that the target is not moving
					// (use cached value)
					controller.Target.Position = PositionLookup[controller.Target.Target].Value;

					if (position.IsInActionRange(controller.Target.Position))
					{
						ActionEvents.AddNoResize(new ActionEventBufferElement
						{
							Action = controller.Target,
							Source = entity,
						});

						// reset
						controller.Target.Target = Entity.Null;
					}
				}
				else
				{
					// reset if picked / destroyed
					controller.Target.Target = Entity.Null;

					// retrieve relevant action types
					ActionType filter = ActionType.All;

					if (pick.Picked != Entity.Null)
					{
						filter &= ~ActionType.Pick;
					}
					else
					{
						filter &= ~ActionType.Store;
					}

					// look for target
					if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, filter, out RoomElementBufferElement target))
					{
						// consider which action to do in priority
						// TBD: improve
						ActionType actionType = 0;
						if (Utilities.HasActionType(target.ActionType, ActionType.Pick))
						{
							actionType = ActionType.Pick;
						}
						else if (Utilities.HasActionType(target.ActionType, ActionType.Store))
						{
							actionType = ActionType.Store;
						}

						if (actionType != 0)
						{
							if (position.IsInActionRange(target.Position))
							{
								// interact
								ActionEvents.AddNoResize(new ActionEventBufferElement
								{
									Action = new ActionTarget(target.Entity, actionType, target.Position),
									Source = entity,
								});
							}
							else
							{
								// track
								controller.Target = new ActionTarget(target.Entity, actionType, target.Position);
							}
						}
					}
				}
			}
		}

		[BurstCompile]
		public partial struct AIMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PositionComponent position, in AIController controller)
			{
				if (controller.HasTarget)
				{
					float2 direction = controller.Target.Position - position.Value;
					movement.Input = math.lengthsq(direction) > math.pow(Const.ActionRange, 2f) ? math.normalize(direction) : float2.zero;
				}
				else
				{
					movement.Input = float2.zero;
				}
			}
		}

	}
}