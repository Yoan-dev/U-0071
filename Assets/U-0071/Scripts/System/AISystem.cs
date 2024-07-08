using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem, ISystemStartStop
	{
		private NativeQueue<ActionEvent>.ParallelWriter _actionEventWriter; // from ActionSystem
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
				.WithAllRW<AIController, Orientation>()
				.WithAll<PositionComponent, PartitionComponent, CreditsComponent>()
				.WithPresent<PickComponent>()
				.Build();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_positionLookup = state.GetComponentLookup<PositionComponent>(true);
			_pickableLookup = state.GetComponentLookup<PickableComponent>(true);
		}

		public void OnStartRunning(ref SystemState state)
		{
			_actionEventWriter = Utilities.GetSystem<ActionSystem>(ref state).EventQueueWriter;
		}

		public void OnStopRunning(ref SystemState state)
		{
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
					ActionEventWriter = _actionEventWriter,
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
			[WriteOnly]
			public NativeQueue<ActionEvent>.ParallelWriter ActionEventWriter;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<PositionComponent> PositionLookup;
			[ReadOnly]
			public ComponentLookup<PickableComponent> PickableLookup;

			public void Execute(
				Entity entity,
				ref AIController controller,
				ref Orientation orientation,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition,
				in CreditsComponent credits)
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

					if (position.IsInRange(controller.Target.Position, controller.Target.Range))
					{
						ActionEventWriter.Enqueue(new ActionEvent(entity, in controller.Target));

						orientation.Update(controller.Target.Position.x - position.Value.x);

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
						filter &= ~ActionType.Trash;
					}

					// look for target
					if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, filter, credits.Value, out RoomElementBufferElement target))
					{
						// consider which action to do in priority
						ActionType actionType = 0;
						if (CanExecuteAction(ActionType.Buy, filter, in target))
						{
							actionType = ActionType.Buy;
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
							if (position.IsInRange(target.Position, target.Range))
							{
								// interact
								ActionEventWriter.Enqueue(new ActionEvent(entity, target.ToActionData(actionType)));
								orientation.Update(target.Position.x - position.Value.x);
							}
							else
							{
								// track
								controller.Target = target.ToActionData(actionType);
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
		public partial struct AIMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PositionComponent position, in AIController controller)
			{
				if (controller.HasTarget)
				{
					float2 direction = controller.Target.Position - position.Value;
					movement.Input = math.lengthsq(direction) > math.pow(controller.Target.Range, 2f) ? math.normalize(direction) : float2.zero;
				}
				else
				{
					movement.Input = float2.zero;
				}
			}
		}

	}
}