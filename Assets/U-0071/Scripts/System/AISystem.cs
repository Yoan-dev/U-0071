using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting.FullSerializer;
using UnityEngine.Rendering.Universal;
using Random = Unity.Mathematics.Random;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(RoomSystem))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<RoomComponent> _roomLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private ComponentLookup<DoorComponent> _doorLookup;
		private EntityQuery _query;
		private float _timer;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Partition>();
			state.RequireForUpdate<FlowfieldCollection>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<AIController>()
				.WithAllRW<ActionController, Orientation>()
				.WithAll<PositionComponent, PartitionComponent, CreditsComponent, HungerComponent>()
				.WithPresent<CarryComponent, DeathComponent, PushedComponent>()
				.WithPresentRW<IsActing>()
				.Build();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_roomLookup = state.GetComponentLookup<RoomComponent>(true);
			_interactableLookup = state.GetComponentLookup<InteractableComponent>(true);
			_pickableLookup = state.GetComponentLookup<PickableComponent>(true);
			_doorLookup = state.GetComponentLookup<DoorComponent>(true);
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
				_roomLookup.Update(ref state);
				_pickableLookup.Update(ref state);
				_doorLookup.Update(ref state);

				state.Dependency = new AIUnitInitJob
				{
					Partition = SystemAPI.GetSingleton<Partition>(),
					Config = SystemAPI.GetSingleton<Config>(),
				}.ScheduleParallel(state.Dependency);

				state.Dependency = new AIActionJob
				{
					RoomElementBufferLookup = _roomElementLookup,
					InteractableLookup = _interactableLookup,
					RoomLookup = _roomLookup,
					PickableLookup = _pickableLookup,
					DoorLookup = _doorLookup,
					DeltaTime = Const.AITick,
				}.ScheduleParallel(_query, state.Dependency);

				state.Dependency = new AIMovementJob
				{
					FlowfieldCollection = SystemAPI.GetSingleton<FlowfieldCollection>(),
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
			public ComponentLookup<RoomComponent> RoomLookup;
			[ReadOnly]
			public ComponentLookup<PickableComponent> PickableLookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup;
			[ReadOnly]
			public ComponentLookup<DoorComponent> DoorLookup;
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

				// attempt at a rough mid-term goal AI
				float hungerRatio = hunger.Value / Const.MaxHunger;
				if (hungerRatio <= Const.AIStarvingRatio)
				{
					// starving
					// go to closest eating space and buy or hope for a meal drop
					controller.Goal = AIGoal.Eat;
					if (carry.HasItem && !Utilities.HasItemFlag(carry.Flags, ItemFlag.Food))
					{
						Utilities.QueueDropAction(ref actionController, ref orientation, in position, in carry, isActing);
					}
				}
				else if (controller.ShouldReassess(hungerRatio, carry.HasItem))
				{
					controller.EatWeight = hungerRatio <= Const.AILightHungerRatio && credits.Value >= Const.AIDesiredCreditsToEat ?
						1f - math.unlerp(Const.AIStarvingRatio, Const.AILightHungerRatio, hungerRatio) : 0f;

					// TODO multiply by level
					int creditsTarget = Const.AIDesiredCreditsPerLevel;
					controller.WorkWeight = credits.Value < creditsTarget ? (1f - credits.Value / creditsTarget) : 0f;

					controller.RelaxWeight = Const.AIRelaxWeight;

					controller.ChooseGoal(carry.HasItem, in RoomLookup, partition.CurrentRoom);
				}

				if (controller.Goal == AIGoal.Relax)
				{
					// life is good
					// TODO: go to a cozy place
					return;
				}

				// look for new target

				bool fleeGoal = controller.Goal == AIGoal.Flee;
				bool eatGoal = controller.Goal == AIGoal.Eat;
				bool workGoal = controller.Goal == AIGoal.Work;
				bool destroyGoal = controller.Goal == AIGoal.Destroy;

				// retrieve relevant action types
				ActionFlag actionFilter =
					fleeGoal ? 0 :
					eatGoal ? ActionFlag.Pick | ActionFlag.Eat | ActionFlag.Collect :
					workGoal ? ActionFlag.Pick | ActionFlag.Store | ActionFlag.Destroy | ActionFlag.Collect | ActionFlag.Search :
					destroyGoal ? ActionFlag.Destroy :
					ActionFlag.Search;
				ItemFlag itemFilter =
					eatGoal ? ItemFlag.Food :
					workGoal ? ItemFlag.RawFood | ItemFlag.Trash : 0;

				if (carry.HasItem)
				{
					actionFilter &= ~ActionFlag.Pick;

					// consider picked interactable action
					if (Utilities.HasItemFlag(carry.Flags, ItemFlag.Food))
					{
						// start interacting
						isActing.ValueRW = true;
						actionController.Action = new ActionData(carry.Picked, ActionFlag.Eat, 0, carry.Flags, position.Value, 0f, carry.Time, 0);
						actionController.Start();
						orientation.Update(actionController.Action.Position.x - position.Value.x);
						return;
					}
				}

				// look for target
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				float minMagn = float.MaxValue;
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						RoomElementBufferElement target = enumerator.Current;

						if (target.HasActionFlag(ActionFlag.Open))
						{
							// we check relative position to door thanks to last movement input
							// (AI always path vertically/horizontally in door "rooms")
							DoorComponent door = DoorLookup[target.Entity];
							bool shouldEnterCode =
								door.CodeRequirementFacing.x != 0f && controller.LastMovementInput.x == -door.CodeRequirementFacing.x ||
								door.CodeRequirementFacing.y != 0f && controller.LastMovementInput.y == -door.CodeRequirementFacing.y;
							if (shouldEnterCode ||
								controller.LastMovementInput.x == door.CodeRequirementFacing.x ||
								controller.LastMovementInput.y == door.CodeRequirementFacing.y)
							{
								actionController.Action = target.ToActionData(ActionFlag.Open, target.ItemFlags, carry.Flags);
								if (shouldEnterCode)
								{
									// override time if needs to enter code
									// TODO: start other stuff
									actionController.Action.Time = Const.AIUnitEnterCodeTime;
								}
								break;
							}
						}
						if (target.Entity != entity &&
							target.HasActionFlag(actionFilter) &&
							(itemFilter == 0 || target.HasItemFlag(itemFilter)) &&
							(target.Cost <= 0f || target.Cost <= credits.Value) &&
							target.Evaluate(actionController.Action.ActionFlag, actionFilter, carry.Flags, out ActionFlag selectedActionFlag, destroyGoal, eatGoal))
						{
							float magn = math.lengthsq(position.Value - target.Position);

							// retrieve closest of prioritary type
							if (eatGoal && selectedActionFlag == ActionFlag.Pick && actionController.Action.ActionFlag != ActionFlag.Pick || 
								selectedActionFlag > actionController.Action.ActionFlag || 
								magn < minMagn)
							{
								// pose as a storage
								if (selectedActionFlag == ActionFlag.Store && target.Interactable.HasActionFlag(ActionFlag.Teleport))
								{
									selectedActionFlag = ActionFlag.Teleport;
								}
								minMagn = magn;
								actionController.Action = target.ToActionData(selectedActionFlag, target.ItemFlags, carry.Flags);

								// found something to do
								controller.IsPathing = false;
							}
							// lower prio would have been filtered in target.Evaluate
						}
					}
				}

				if (actionController.HasTarget && carry.HasItem && 
					actionController.Action.HasActionFlag(ActionFlag.Collect | ActionFlag.Pick))
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
					if (carry.HasItem && !controller.HasCriticalGoal)
					{
						controller.Goal = AIGoal.Destroy;
					}

					// start/continue pathing (flowfield)
					controller.IsPathing = true;
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
			public FlowfieldCollection FlowfieldCollection;

			public void Execute(ref MovementComponent movement, ref AIController controller, in PositionComponent position, in ActionController actionController, in AuthorisationComponent authorisation)
			{
				movement.IsRunning = controller.Goal == AIGoal.Flee;

				if (controller.IsPathing)
				{
					movement.Input = FlowfieldCollection.GetDirection(authorisation.AreaFlag, controller.Goal, position.Value);
					if (movement.Input.Equals(float2.zero))
					{
						// arrived
						controller.IsPathing = false;
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

				controller.LastMovementInput = movement.Input;
			}
		}
	}

	[BurstCompile]
	[WithAll(typeof(AIController))]
	public partial struct AIUnitInitJob : IJobEntity
	{
		[ReadOnly]
		public Partition Partition;
		public Config Config;

		public void Execute(
			Entity entity,
			ref AuthorisationComponent authorization,
			ref NameComponent name,
			ref SkinColor skin,
			ref ShortHairColor shortHair,
			ref LongHairColor longHair,
			ref BeardColor beard,
			ref PilosityComponent pilosity,
			ref ShirtColor shirt,
			in PositionComponent position,
			EnabledRefRW<AIUnitInitTag> initTag)
		{
			initTag.ValueRW = false;

			authorization.AreaFlag = Utilities.GetLowestAuthorization(Partition.GetAuthorization(position.Value));

			UnitIdentity identity = Config.UnitNames.Value.Identities[entity.Index % Config.UnitNames.Value.Identities.Length];

			name.Value = identity.Name;
			pilosity.HasShortHair = identity.HasShortHair;
			pilosity.HasLongHair = identity.HasLongHair;
			pilosity.HasBeard = identity.HasBeard;

			float4 skinColor = Config.UnitRenderingColors.Value.SkinColors[identity.SkinColorIndex];
			float4 hairColor = Config.UnitRenderingColors.Value.HairColors[identity.HairColorIndex];

			skin.Value = skinColor;
			shortHair.Value = pilosity.HasShortHair ? hairColor : skinColor;
			longHair.Value = pilosity.HasLongHair ? hairColor : skinColor;
			beard.Value = pilosity.HasBeard ? hairColor : skinColor;

			shirt.Value =
				authorization.AreaFlag == AreaAuthorization.LevelOne ? Config.LevelOneShirtColor :
				authorization.AreaFlag == AreaAuthorization.LevelTwo ? Config.LevelTwoShirtColor :
				authorization.AreaFlag == AreaAuthorization.LevelThree ? Config.LevelThreeShirtColor : float4.zero;
		}
	}
}