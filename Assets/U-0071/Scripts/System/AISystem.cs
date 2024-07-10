using System;
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
			state.RequireForUpdate<Partition>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<AIController>()
				.WithAllRW<ActionController, Orientation>()
				.WithAll<PositionComponent, PartitionComponent, CreditsComponent, HungerComponent>()
				.WithPresent<CarryComponent, DeathComponent, PushedComponent>()
				.WithPresentRW<IsActing>()
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
					DeltaTime = Const.AITick,
				}.ScheduleParallel(_query, state.Dependency);

				state.Dependency = new AIMovementJob
				{
					Flowfield = SystemAPI.GetSingleton<Flowfield>(),
				}.ScheduleParallel(state.Dependency);
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
			public float DeltaTime;

			public void Execute(
				Entity entity,
				ref AIController controller,
				ref ActionController actionController,
				ref Orientation orientation,
				in PositionComponent position,
				in CarryComponent carry,
				in PartitionComponent partition,
				in CreditsComponent credits,
				in HungerComponent hunger,
				EnabledRefRW<IsActing> isActing,
				EnabledRefRO<DeathComponent> death,
				EnabledRefRO<PushedComponent> pushed)
			{
				if (Utilities.ProcessUnitControllerStart(ref actionController, ref orientation, in position, in carry, in partition, isActing, death, pushed))
				{
					return;
				}

				controller.ReassessmentTimer -= DeltaTime;

				// re-evaluate current target
				if (actionController.HasTarget)
				{
					if (!InteractableLookup.TryGetComponent(actionController.Action.Target, out InteractableComponent interactable) ||
						interactable.HasActionFlag(ActionFlag.Pick) && PickableLookup.IsComponentEnabled(actionController.Action.Target) ||
						!interactable.HasActionFlag(actionController.Action.ActionFlag))
					{
						// target has been destroyed/picked/disabled
						actionController.Stop();
					}
					else if (position.IsInRange(actionController.Action.Position, actionController.Action.Range))
					{
						// start interacting
						isActing.ValueRW = true;
						actionController.Start();
						orientation.Update(actionController.Action.Position.x - position.Value.x);
						return;
					}

					// going to target
					if (actionController.HasTarget)
					{
						return;
					}
				}

				if (controller.RoomPathingTimer > 0f)
				{
					// take time to move to another room
					controller.RoomPathingTimer -= DeltaTime;
					return;
				}

				// attempt at a rough mid-term goal AI
				float hungerRatio = hunger.Value / Const.MaxHunger;
				if (hungerRatio <= Const.AIStarvingRatio)
				{
					// starving
					// go to closest eating space and buy or hope for a meal drop
					controller.Goal = AIGoal.Eat;
					if (carry.Picked != Entity.Null && !Utilities.HasItemFlag(carry.Flags, ItemFlag.Food))
					{
						Utilities.QueueDropAction(ref actionController, ref orientation, in position, in carry, isActing);
					}
				}
				else if (controller.ShouldReassess(hungerRatio))
				{
					controller.ReassessmentTimer = Const.AIGoalReassessmentTime;

					controller.EatWeight = hungerRatio <= Const.AILightHungerRatio && credits.Value >= Const.AIDesiredCreditsToEat ?
						1f - math.unlerp(Const.AIStarvingRatio, Const.AILightHungerRatio, hungerRatio) : 0f;

					// TODO multiply by level
					int creditsTarget = Const.AIDesiredCreditsPerLevel;
					controller.WorkWeight = credits.Value < creditsTarget ? (1f - credits.Value / creditsTarget) : 0f;

					// TODO boredom factor
					controller.RelaxWeight = Const.AIRelaxWeight;
					controller.WanderWeight = Const.AIWanderWeight;

					controller.ChoseGoal();
				}

				if (controller.Goal == AIGoal.Relax)
				{
					// life is good
					// TODO: go to a cozy place
					return;
				}

				// look for new target

				// retrieve relevant action types
				ActionFlag actionFilter =
					controller.Goal == AIGoal.Eat ? ActionFlag.Pick | ActionFlag.Eat | ActionFlag.Collect :
					controller.Goal == AIGoal.Work ? ActionFlag.Pick | ActionFlag.Store | ActionFlag.Destroy | ActionFlag.Collect | ActionFlag.Search :
					ActionFlag.Search;
				ItemFlag itemFilter = controller.Goal == AIGoal.Eat ? ItemFlag.Food : 0;

				if (carry.Picked != Entity.Null)
				{
					actionFilter &= ~ActionFlag.Pick;

					// consider picked interactable action
					if (Utilities.HasItemFlag(carry.Flags, ItemFlag.Food))
					{
						// start interacting
						isActing.ValueRW = true;
						actionController.Action = new ActionData(carry.Picked, ActionFlag.Eat, position.Value, 0f, carry.Time, 0);
						actionController.Start();
						orientation.Update(actionController.Action.Position.x - position.Value.x);
						return;
					}
				}

				// look for target
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				float minMagn = float.MaxValue;
				bool eatGoal = controller.Goal == AIGoal.Eat;
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						RoomElementBufferElement target = enumerator.Current;
						if (target.Entity != entity &&
							target.HasActionFlag(actionFilter) &&
							(itemFilter == 0 || target.HasItemFlag(itemFilter)) &&
							(target.Cost <= 0f || target.Cost <= credits.Value) &&
							target.Evaluate(actionController.Action.ActionFlag, actionFilter, carry.Flags, out ActionFlag selectedActionFlag, false, eatGoal))
						{
							float magn = math.lengthsq(position.Value - target.Position);

							// retrieve closest of prioritary type
							if (eatGoal && selectedActionFlag == ActionFlag.Pick && actionController.Action.ActionFlag != ActionFlag.Pick || 
								selectedActionFlag > actionController.Action.ActionFlag || 
								magn < minMagn)
							{
								minMagn = magn;
								actionController.Action = target.ToActionData(selectedActionFlag);
							}
							// lower prio would have been filtered in target.Evaluate
						}
					}
				}

				if (actionController.HasTarget && carry.Picked != Entity.Null && actionController.Action.ActionFlag == ActionFlag.Collect)
				{
					// will not be able to interact with target
					// drop item to be able on next tick
					Utilities.QueueDropAction(ref actionController, ref orientation, in position, in carry, isActing);
				}
				else if (actionController.HasTarget && position.IsInRange(actionController.Action.Position, actionController.Action.Range))
				{
					// immediately interact
					isActing.ValueRW = true;
					actionController.Start();
					orientation.Update(actionController.Action.Position.x - position.Value.x);
				}

				if (!actionController.HasTarget)
				{
					// move to another room (flowfield)
					controller.RoomPathingTimer = Const.AIRoomPathingTime;
				}
			}
		}

		[BurstCompile]
		[WithAll(typeof(AIController))]
		[WithNone(typeof(DeathComponent))]
		[WithNone(typeof(PushedComponent))]
		public partial struct AIMovementJob : IJobEntity
		{
			[ReadOnly]
			public Flowfield Flowfield;

			public void Execute(ref MovementComponent movement, ref AIController controller, in PositionComponent position, in ActionController actionController)
			{
				if (controller.RoomPathingTimer > 0f)
				{
					movement.Input = Flowfield.GetDirection(controller.Goal, position.Value);
					if (movement.Input.Equals(float2.zero))
					{
						// arrived
						controller.RoomPathingTimer = 0f;
					}
				}
				else if (!actionController.IsResolving && actionController.HasTarget)
				{
					float2 direction = actionController.Action.Position - position.Value;
					movement.Input = math.lengthsq(direction) > math.pow(actionController.Action.Range, 2f) ? math.normalize(direction) : float2.zero;
				}
				else
				{
					movement.Input = float2.zero;
				}
			}
		}

	}
}