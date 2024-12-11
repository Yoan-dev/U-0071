using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(RoomSystem))]
	[UpdateBefore(typeof(ActionSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		private EntityQuery _query;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
			state.RequireForUpdate<Partition>();
			state.RequireForUpdate<FlowfieldCollection>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<AIController>()
				.WithAllRW<ActionController, Orientation>()
				.WithAll<PositionComponent, PartitionInfoComponent, CreditsComponent, HungerComponent, AuthorizationComponent, ContaminationLevelComponent>()
				.WithPresent<CarryComponent, DeathComponent, PushedComponent>()
				.WithPresentRW<IsActing>()
				.Build();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new AIUnitInitJob
			{
				Partition = SystemAPI.GetSingleton<Partition>(),
				Config = SystemAPI.GetSingleton<Config>(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new AIActionJob
			{
				RoomElementBufferLookup = SystemAPI.GetBufferLookup<RoomElementBufferElement>(true),
				InteractableLookup = SystemAPI.GetComponentLookup<InteractableComponent>(true),
				RoomLookup = SystemAPI.GetComponentLookup<RoomComponent>(true),
				WorkInfoLookup = SystemAPI.GetComponentLookup<WorkInfoComponent>(true),
				PickableLookup = SystemAPI.GetComponentLookup<PickableComponent>(true),
				DoorLookup = SystemAPI.GetComponentLookup<DoorComponent>(true),
				Cycle = SystemAPI.GetSingleton<CycleComponent>(),
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(_query, state.Dependency);

			state.Dependency = new AIMovementJob
			{
				FlowfieldCollection = SystemAPI.GetSingleton<FlowfieldCollection>(),
			}.ScheduleParallel(state.Dependency);
		}
		
		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct AIActionJob : IJobEntity
		{
			[ReadOnly] public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly] public ComponentLookup<RoomComponent> RoomLookup;
			[ReadOnly] public ComponentLookup<WorkInfoComponent> WorkInfoLookup;
			[ReadOnly] public ComponentLookup<PickableComponent> PickableLookup;
			[ReadOnly] public ComponentLookup<InteractableComponent> InteractableLookup;
			[ReadOnly] public ComponentLookup<DoorComponent> DoorLookup;
			public CycleComponent Cycle;
			public float DeltaTime;

			public void Execute(
				Entity entity,
				ref AIController controller,
				ref ActionController actionController,
				ref Orientation orientation,
				in PositionComponent position,
				in CarryComponent carry,
				in PartitionInfoComponent partitionInfo,
				in CreditsComponent credits,
				in AuthorizationComponent authorization,
				in HungerComponent hunger,
				in ContaminationLevelComponent contaminationLevel,
				EnabledRefRW<IsActing> isActing,
				EnabledRefRO<DeathComponent> death,
				EnabledRefRO<PushedComponent> pushed)
			{
				// post gamejam thoughts
				// - should be split in different jobs
				// - should have more logic shared with player

				bool isAdmin = authorization.IsAdmin;

				// AI timers
				bool isInActivity = actionController.IsResolving && !isAdmin || controller.Goal == AIGoal.Destroy || controller.Goal == AIGoal.Process || controller.Goal == AIGoal.WorkWander || controller.Goal == AIGoal.BoredWander;
				controller.BoredomValue = math.clamp(controller.BoredomValue + DeltaTime * (isInActivity ? Const.AIFulfilmentSpeed : Const.AIBoredomSpeed), 0f, Const.AIMaxBoredomWeight);
				controller.SuspicionValue = math.max(0f, controller.SuspicionValue - DeltaTime * Const.PeekingSuspicionDecreaseRate);
				controller.ReassessmentTimer -= DeltaTime;
				controller.ReassessedLastFrame = false;
				controller.CantReachTimer = actionController.HasTarget && !actionController.IsResolving ? controller.CantReachTimer : 0f;
				controller.Awareness = Utilities.GetCurrentAwareness(contaminationLevel.IsSick, controller.Goal);

				if (Cycle.CycleChanged && actionController.IsResolving && actionController.Action.ActionFlag == ActionFlag.Open)
				{
					// stop interacting with door if cycle changed (code changed)
					actionController.Stop(death.ValueRO || pushed.ValueRO, false);
					return;
				}

				if (actionController.IsResolving && controller.Goal == AIGoal.Work)
				{
					// found a job
					controller.OpportunityFlag = false;
				}

				if (pushed.ValueRO)
				{
					controller.LastMovementInput = float2.zero;
				}

				if (Utilities.ProcessUnitControllerStart(entity, ref actionController, in orientation, in position, in partitionInfo, isActing, death, pushed, in InteractableLookup, in PickableLookup, in carry))
				{
					// return early if the AI behaviour is stopped (dead, pushed, ...)
					return;
				}

				// re-evaluate current target
				if (actionController.HasTarget)
				{
					if (position.IsInRange(actionController.Action.Position, actionController.Action.Range))
					{
						// start interacting
						isActing.ValueRW = true;
						actionController.Start();
						orientation.Update(actionController.Action.Position.x - position.Value.x);
					}

					// safe
					controller.CantReachTimer += DeltaTime;
					if (controller.CantReachTimer >= Const.AICantReachTime)
					{
						actionController.Stop(false, false);
					}

					// start interacting or going to target
					return;
				}

				bool isFired = false;

				// look for job opportunities
				if (controller.Goal == AIGoal.WorkWander && WorkInfoLookup.HasComponent(partitionInfo.CurrentRoom))
				{
					WorkInfoComponent workInfo = WorkInfoLookup[partitionInfo.CurrentRoom];
					controller.OpportunityFlag = workInfo.ShouldStopHere(authorization.Flag);
				}

				// check if in a crowded workplace and fired
				if (controller.Goal == AIGoal.Work)
				{
					RoomComponent room = RoomLookup[partitionInfo.CurrentRoom];
					if (room.Capacity > 0 && room.Population > room.Capacity && room.FiredWorker == entity && Utilities.GetLowestAuthorization(room.Authorization) == authorization.Flag)
					{
						controller.OpportunityFlag = false; // consume flag
						isFired = true;
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
						Utilities.QueueDropAction(ref actionController, in orientation, in position, in carry, in partitionInfo, isActing);
					}
				}
				else if (controller.ShouldReassess(carry.HasItem, isFired))
				{
					controller.ReassessedLastFrame = true;

					// compute goal weights and reassess current goal

					bool shouldEat = hungerRatio <= Const.AILightHungerRatio && credits.Value >= Const.AIDesiredCreditsToEat;
					controller.EatWeight = shouldEat ? math.clamp(1f - math.unlerp(Const.AIStarvingRatio, Const.AILightHungerRatio, hungerRatio), 0f, 1f) : 0f;

					int classCredits = Const.GetStartingCredits(authorization.Flag);
					float classCreditsRatio = !isAdmin && classCredits > 0f ? 1f - credits.Value / (float)classCredits : 0f;
					float contaminationLevelModifier = contaminationLevel.Value > 0 ? math.clamp(contaminationLevel.Value / Const.ContaminationSickTreshold, 0f, 1f) * Const.SickAntiworkWeight : 0f;
					controller.WorkWeight = math.clamp(Const.AIBaseWorkWeight - contaminationLevelModifier + classCreditsRatio * (1f - Const.AIBaseWorkWeight), 0f, 1f);

					controller.ChooseGoal(carry.HasItem, isFired);
				}

				// look for new target

				bool eatGoal = controller.Goal == AIGoal.Eat;
				bool workGoal = controller.Goal == AIGoal.Work;
				bool destroyGoal = controller.Goal == AIGoal.Destroy;

				// retrieve relevant action and item types depending on current goal
				ActionFlag actionFilter = controller.Goal switch
				{
					AIGoal.Flee => 0,
					AIGoal.Eat => ActionFlag.Pick | ActionFlag.Eat | ActionFlag.Collect,
					AIGoal.Work => isAdmin ? ActionFlag.Administrate : ActionFlag.Pick | ActionFlag.Store | ActionFlag.Destroy | ActionFlag.Collect | ActionFlag.Search,
					AIGoal.Destroy => ActionFlag.Destroy,
					AIGoal.Process => ActionFlag.Store,
					_ => ActionFlag.Search
				};
				ItemFlag itemFilter = controller.Goal switch
				{
					AIGoal.Eat => ItemFlag.Food,
					AIGoal.Work => !isAdmin ? ItemFlag.RawFood | ItemFlag.Trash : 0,
					_ => 0
				};

				bool adminCleaning = false;
				if (isAdmin && (workGoal || destroyGoal))
				{
					bool isInAdminRoom = RoomLookup[partitionInfo.CurrentRoom].Authorization == AreaAuthorization.Admin;

					// admins are forced to clean their room
					if (isInAdminRoom && !carry.HasItem)
					{
						adminCleaning = true;
						actionFilter |= ActionFlag.Pick;
						actionFilter |= ActionFlag.Destroy;
						itemFilter |= ItemFlag.Trash;
						itemFilter |= ItemFlag.RawFood;
					}
					else if (carry.HasItem && !isInAdminRoom)
					{
						// drop outside
						Utilities.QueueDropAction(ref actionController, in orientation, in position, in carry, in partitionInfo, isActing);
					}
					else if (carry.HasItem && !controller.HasCriticalGoal)
					{
						actionFilter &= ~ActionFlag.Administrate;
						controller.Goal = AIGoal.Destroy;
						controller.IsPathing = true;
					}
				}

				if (!eatGoal && hunger.Value <= Const.MaxHunger * Const.AILightHungerRatio)
				{
					// opportunistic eating
					actionFilter |= ActionFlag.Eat;
				}

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
						controller.EatFlag = true;
						return;
					}
					else if (controller.Goal == AIGoal.Eat)
					{
						Utilities.QueueDropAction(ref actionController, in orientation, in position, in carry, in partitionInfo, isActing);
					}
				}

				// look for target in current room
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partitionInfo.CurrentRoom];
				float minMagn = float.MaxValue;
				bool teleportItemFlag = false;
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						RoomElementBufferElement target = enumerator.Current;

						// door interaction (special case)
						if (!target.Interactable.IsIgnored && target.CanBeUsed && target.HasActionFlag(ActionFlag.Open))
						{
							DoorComponent door = DoorLookup[target.Entity];
							bool isOnCodeSide = door.IsOnEnterCodeSide(position.Value, target.Position);

							if (door.CodeRequirementFacing.x != 0f && (door.CodeRequirementFacing.x == controller.LastMovementInput.x * (isOnCodeSide ? -1f : 1f)) ||
								door.CodeRequirementFacing.y != 0f && (door.CodeRequirementFacing.y == controller.LastMovementInput.y * (isOnCodeSide ? -1f : 1f)))
							{
								actionController.Action = target.ToActionData(ActionFlag.Open, target.ItemFlags, carry.Flags);
								if (isOnCodeSide)
								{
									// override time if needs to enter code
									actionController.Action.Time = Const.AIUnitEnterCodeTime;
								}
								break;
							}
						}

						// other interactions
						if (target.Entity != entity &&
							!target.Interactable.IsIgnored &&
							target.CanBeUsed &&
							target.HasActionFlag(actionFilter) &&
							(itemFilter == 0 || target.HasItemFlag(itemFilter)) &&
							(target.Cost <= 0f || target.Cost <= credits.Value) &&
							target.Evaluate(actionController.Action.ActionFlag, actionFilter, carry.Flags, out ActionFlag selectedActionFlag, destroyGoal, eatGoal || adminCleaning, false, false))
						{
							float magn = math.lengthsq(position.Value - target.Position);

							// retrieve closest of prioritary type
							if (eatGoal && selectedActionFlag == ActionFlag.Pick && actionController.Action.ActionFlag != ActionFlag.Pick || 
								selectedActionFlag > actionController.Action.ActionFlag || 
								magn < minMagn)
							{
								// item teleporters pose as storage
								// (because of spaghetti code)
								teleportItemFlag = selectedActionFlag == ActionFlag.Store && target.Interactable.HasActionFlag(ActionFlag.TeleportItem);
								
								minMagn = magn;
								actionController.Action = target.ToActionData(selectedActionFlag, target.ItemFlags, carry.Flags);

								// found something to do
								controller.IsPathing = false;
							}
							// lower prio would have been filtered in target.Evaluate
						}
					}
				}

				if (teleportItemFlag)
				{
					actionController.Action.ActionFlag = ActionFlag.TeleportItem;
				}

				if (actionController.HasTarget && carry.HasItem && 
					actionController.Action.HasActionFlag(ActionFlag.Collect | ActionFlag.Pick))
				{
					// will not be able to interact with target
					// drop item to be able on next tick
					Utilities.QueueDropAction(ref actionController, in orientation, in position, in carry, in partitionInfo, isActing);
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
						// if not busy, carry hold item to processor/trash
						controller.Goal = Utilities.HasItemFlag(carry.Flags, ItemFlag.RawFood) ? AIGoal.Process : AIGoal.Destroy;
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
			[ReadOnly] public FlowfieldCollection FlowfieldCollection;

			public void Execute(ref MovementComponent movement, ref AIController controller, in PositionComponent position, in ActionController actionController, in AuthorizationComponent authorisation)
			{
				movement.IsRunning = controller.Goal == AIGoal.Flee;
				
				if (!actionController.IsResolving && actionController.HasTarget)
				{
					// go to target
					float2 direction = actionController.Action.Position - position.Value;
					movement.Input = math.lengthsq(direction) > math.pow(actionController.Action.Range, 2f) ? math.normalize(direction) : float2.zero;
				}
				else if (!actionController.IsResolving && controller.IsPathing)
				{
					// retrieve direction from flowfield
					movement.Input = FlowfieldCollection.GetDirection(authorisation.Flag, controller.Goal, position.Value);
					if (movement.Input.Equals(float2.zero))
					{
						// arrived
						controller.IsPathing = false;
					}
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
		[ReadOnly] public Partition Partition;
		public Config Config;

		public void Execute(
			Entity entity,
			ref AuthorizationComponent authorization,
			ref CreditsComponent credits,
			ref HungerComponent hunger,
			ref AIController aIController,
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

			UnitIdentity identity = Config.UnitIdentityData.Value.Identities[entity.Index % Config.UnitIdentityData.Value.Identities.Length];
			name.Value = identity.Name;

			// init unit gameplay data
			authorization.Flag = Utilities.GetLowestAuthorization(Partition.GetAuthorization(position.Value));
			credits.AdminCard = authorization.IsAdmin;
			credits.Value = (int)(Const.GetStartingCredits(authorization.Flag) * identity.StartingCreditsRatio);
			hunger.Value = identity.StartingHunger;
			aIController.BoredomValue = identity.StartingBoredom;
			
			// init material property components (for shader)
			pilosity.HasShortHair = identity.HasShortHair;
			pilosity.HasLongHair = identity.HasLongHair;
			pilosity.HasBeard = identity.HasBeard;
			float4 skinColor = Config.UnitIdentityData.Value.SkinColors[identity.SkinColorIndex];
			float4 hairColor = Config.UnitIdentityData.Value.HairColors[identity.HairColorIndex];
			skin.Value = skinColor;
			shortHair.Value = pilosity.HasShortHair ? hairColor : skinColor;
			longHair.Value = pilosity.HasLongHair ? hairColor : skinColor;
			beard.Value = pilosity.HasBeard ? hairColor : skinColor;
			shirt.Value =
				authorization.Flag == AreaAuthorization.LevelOne ? Config.LevelOneShirtColor :
				authorization.Flag == AreaAuthorization.LevelTwo ? Config.LevelTwoShirtColor :
				authorization.Flag == AreaAuthorization.LevelThree ? Config.LevelThreeShirtColor : Config.AdminShirtColor;
		}
	}
}