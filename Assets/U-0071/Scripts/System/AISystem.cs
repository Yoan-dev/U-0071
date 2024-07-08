using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(RoomSystem))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;
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
				.WithNone<IsDeadTag>()
				.Build();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_interactableLookup = state.GetComponentLookup<InteractableComponent>(true);
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
				_interactableLookup.Update(ref state);
				_pickableLookup.Update(ref state);

				state.Dependency = new AIActionJob
				{
					RoomElementBufferLookup = _roomElementLookup,
					InteractableLookup = _interactableLookup,
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
			public ComponentLookup<PickableComponent> PickableLookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup;

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
				// cannot act if not in partition or already acting
				if (partition.CurrentRoom == Entity.Null || controller.IsResolving) return;

				// evaluate current target
				if (controller.HasTarget)
				{
					if (!InteractableLookup.TryGetComponent(controller.Action.Target, out InteractableComponent interactable) ||
						interactable.HasType(ActionType.Pick) && PickableLookup.IsComponentEnabled(controller.Action.Target) ||
						!interactable.HasType(controller.Action.Type))
					{
						// target has been destroyed / picked / exhausted
						controller.Action.Target = Entity.Null;
					}
					else if (position.IsInRange(controller.Action.Position, controller.Action.Range))
					{
						// start interacting
						isActing.ValueRW = true;
						controller.Start();
						orientation.Update(controller.Action.Position.x - position.Value.x);
						return;
					}

					// going to target
					if (controller.Action.Target != Entity.Null)
					{
						return;
					}
				}

				// look for new target

				// reset if picked / destroyed
				controller.Action.Target = Entity.Null;

				// retrieve relevant action types
				ActionType filter = ActionType.Pick | ActionType.Collect;
				ActionType refFilter = 0;

				if (pick.Picked != Entity.Null)
				{
					// consider picked interactable action
					if (Utilities.HasActionType(pick.Flags, ActionType.Eat))
					{
						// start interacting
						isActing.ValueRW = true;
						controller.Action = new ActionData(pick.Picked, ActionType.Eat, position.Value, 0f, pick.Time, 0);
						controller.Start();
						orientation.Update(controller.Action.Position.x - position.Value.x);
						return;
					}

					filter &= ~ActionType.Pick;
					if (Utilities.HasActionType(pick.Flags, ActionType.RefTrash)) filter |= ActionType.Trash;
					if (Utilities.HasActionType(pick.Flags, ActionType.Process))
					{
						filter |= ActionType.Store;
						refFilter |= ActionType.RefProcess;
					}
				}
				else
				{
					filter &= ~ActionType.Store;
					filter &= ~ActionType.Trash;
				}

				// look for target
				if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, filter, refFilter, credits.Value, out RoomElementBufferElement target))
				{
					// consider which action to do in priority
					ActionType actionType = 0;
					if (CanExecuteAction(ActionType.Store, filter, in target))
					{
						actionType = ActionType.Store;
					}
					else if (CanExecuteAction(ActionType.Trash, filter, in target))
					{
						actionType = ActionType.Trash;
					}
					else if (CanExecuteAction(ActionType.Pick, filter, in target))
					{
						actionType = ActionType.Pick;
					}
					else if (CanExecuteAction(ActionType.Collect, filter, in target))
					{
						actionType = ActionType.Collect;
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

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private bool CanExecuteAction(ActionType type, ActionType filter, in RoomElementBufferElement target)
			{
				// note: credits vs cost already checked during query
				return Utilities.HasActionType(filter, type) && Utilities.HasActionType(target.ActionFlags, type);
			}
		}

		[BurstCompile]
		[WithAll(typeof(AITag))]
		[WithNone(typeof(IsDeadTag))]
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