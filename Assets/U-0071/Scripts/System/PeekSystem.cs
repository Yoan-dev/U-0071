using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace U0071
{
	public struct PeekingReactionEvent
	{
		public Entity Source;
		public Entity Target;
		public float2 TargetPosition;
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MovementSystem))]
	[UpdateAfter(typeof(HealthSystem))]
	public partial struct PeekSystem : ISystem
	{
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<InteractableComponent> _interactionLookup;
		private ComponentLookup<AuthorizationComponent> _authorizationLookup;
		private ComponentLookup<AIController> _aiLookup;
		private ComponentLookup<ActionController> _actionLookup;
		private NativeQueue<PeekingInfoComponent> _peekingInfos;
		private NativeQueue<PeekingReactionEvent> _peekingReactions;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();

			_peekingInfos = new NativeQueue<PeekingInfoComponent>(Allocator.Persistent);
			_peekingReactions = new NativeQueue<PeekingReactionEvent>(Allocator.Persistent);

			_positionLookup = state.GetComponentLookup<PositionComponent>(true);
			_interactionLookup = state.GetComponentLookup<InteractableComponent>(true);
			_authorizationLookup = state.GetComponentLookup<AuthorizationComponent>(true);
			_aiLookup = state.GetComponentLookup<AIController>();
			_actionLookup = state.GetComponentLookup<ActionController>();
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_peekingInfos.Dispose();
			_peekingReactions.Dispose();
		}
		
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_positionLookup.Update(ref state);
			_interactionLookup.Update(ref state);
			_authorizationLookup.Update(ref state);
			_actionLookup.Update(ref state);
			_aiLookup.Update(ref state);

			state.Dependency = new PeekingJob
			{
				PlayerEntity = SystemAPI.GetSingletonEntity<PlayerController>(),
				PositionLookup = _positionLookup,
				AuthorizationLookup = _authorizationLookup,
				ActionLookup = _actionLookup,
				PeekingInfos = _peekingInfos.AsParallelWriter(),
				PeekingReactions = _peekingReactions.AsParallelWriter(),
				AILookup = _aiLookup,
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new GetPeekingInfoJob
			{
				PeekingInfos = _peekingInfos,
			}.Schedule(state.Dependency);

			state.Dependency = new PeekingReactionJob
			{
				ActionLookup = _actionLookup,
				AILookup = _aiLookup,
				InteractableLookup = _interactionLookup,
				PeekingReactions = _peekingReactions,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct PeekingJob : IJobEntity
		{
			public Entity PlayerEntity;
			[ReadOnly] public ComponentLookup<PositionComponent> PositionLookup;
			[ReadOnly] public ComponentLookup<AuthorizationComponent> AuthorizationLookup;
			[ReadOnly] public ComponentLookup<ActionController> ActionLookup;
			[ReadOnly] public ComponentLookup<AIController> AILookup;
			public NativeQueue<PeekingInfoComponent>.ParallelWriter PeekingInfos;
			public NativeQueue<PeekingReactionEvent>.ParallelWriter PeekingReactions;
			public float DeltaTime;

			[BurstCompile]
			public void Execute(Entity entity, ref PeekingComponent peeking, ref InteractableComponent interactable, in DoorComponent door, in PositionComponent position)
			{
				// not relying on partition here because entities in different rooms cannot detect each other (doorway = single tile room)
				if (peeking.StartedFlag || interactable.CurrentUser != Entity.Null && interactable.CurrentUser != PlayerEntity)
				{
					float2 playerPosition = PositionLookup[PlayerEntity].Value;
					bool isInRange = position.IsInRange(playerPosition, Const.PeekingStartRange) && door.IsOnEnterCodeSide(playerPosition, position.Value);
					bool isPeeking = isInRange && position.IsInRange(playerPosition, Const.PeekingRange);

					if (isInRange && !peeking.StartedFlag && door.IsOnEnterCodeSide(PositionLookup[interactable.CurrentUser].Value, position.Value))
					{
						// init
						ActionController controller = ActionLookup[interactable.CurrentUser];
						peeking.StartedFlag = true;
						peeking.DigitIndex = (int)(5 * controller.Timer / controller.Action.Time);
						peeking.FirstDiscovered = peeking.SecondDiscovered = peeking.ThirdDiscovered = peeking.FourthDiscovered = false;
						peeking.StaysTimer = 0f;
						peeking.Suspicion = AILookup[interactable.CurrentUser].SuspicionValue; // if already busted
					}
					else if (peeking.StartedFlag && interactable.CurrentUser != Entity.Null)
					{
						// progress
						ActionController controller = ActionLookup[interactable.CurrentUser];
						int newDigitIndex = (int)(5 * controller.Timer / controller.Action.Time);
						
						if (isPeeking)
						{
							// TODO: depends on authorization comparison
							// increase suspicion depending on player and user authorization
							peeking.Suspicion += DeltaTime * Const.PeekingSuspicionSpeed * Const.GetSuspicionMultiplier(AuthorizationLookup[PlayerEntity].Flag, AuthorizationLookup[interactable.CurrentUser].Flag);
							if (peeking.Suspicion >= 1f)
							{
								// busted !
								PeekingReactions.Enqueue(new PeekingReactionEvent
								{
									Source = interactable.CurrentUser,
									Target = PlayerEntity,
									TargetPosition = playerPosition,
								});

								// we do this here to not have to schedule an action
								// (can schedule push immediately)
								interactable.CurrentUser = Entity.Null;
								interactable.Changed = true;
							}
						}
						
						if (isPeeking && newDigitIndex != peeking.DigitIndex)
						{
							// reveal typed digit index
							peeking.RevealDigit(peeking.DigitIndex);
						}
						peeking.DigitIndex = newDigitIndex;
					}
					else if (peeking.StartedFlag && interactable.CurrentUser == Entity.Null) // interaction stopped
					{
						peeking.StartedFlag = false;

						if (isPeeking && door.OpenTimer > 0f && peeking.StaysTimer == 0f)
						{
							// was peeking when door opened, reveal last digit
							peeking.FourthDiscovered = true;
							peeking.DigitIndex = 4;
						}
						// else interaction was cancelled
					}

					if (peeking.StartedFlag && isInRange)
					{
						PeekingInfos.Enqueue(new PeekingInfoComponent
						{
							DoorEntity = entity,
							Authorization = door.AreaFlag,
							Peeking = peeking,
							Position = position.Value,
							IsPeeking = isPeeking,
						});
					}
				}
			}
		}

		[BurstCompile]
		public partial struct GetPeekingInfoJob : IJobEntity
		{
			public NativeQueue<PeekingInfoComponent> PeekingInfos;

			public void Execute(ref PeekingInfoComponent info, in PositionComponent position)
			{
				info = new PeekingInfoComponent();
				float minMagn = float.MaxValue;
				while (PeekingInfos.Count > 0)
				{
					PeekingInfoComponent checkedInfo = PeekingInfos.Dequeue();
					float magn = math.lengthsq(position.Value - checkedInfo.Position);
					if (magn < minMagn)
					{
						info = checkedInfo;
						minMagn = magn;
					}
				}
				if (info.DoorEntity != Entity.Null && !position.Value.Equals(info.Position))
				{
					info.DistanceRatio = math.lengthsq(position.Value - info.Position) / math.pow(Const.PeekingStartRange, 2f);
				}
			}
		}

		[BurstCompile]
		public partial struct PeekingReactionJob : IJob
		{
			public NativeQueue<PeekingReactionEvent> PeekingReactions;
			public ComponentLookup<ActionController> ActionLookup;
			public ComponentLookup<AIController> AILookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup;

			public void Execute()
			{
				while (PeekingReactions.Count > 0)
				{
					PeekingReactionEvent peekingReaction = PeekingReactions.Dequeue();

					ref ActionController controller = ref ActionLookup.GetRefRW(peekingReaction.Source).ValueRW;

					// manual reset, ideally we would have 
					// a generic way to queue new actions
					// currently have to send "should stop" then wait the next frame to do something

					controller.Action.Target = Entity.Null;
					controller.Action.ActionFlag = 0;
					controller.Timer = 0;
					controller.IsResolving = false;
					controller.ShouldStopFlag = false;

					InteractableComponent playerInteractable = InteractableLookup[peekingReaction.Target];

					// push the player and become more suspicious (will decrease over time)
					AILookup.GetRefRW(peekingReaction.Source).ValueRW.SuspicionValue = 1f;
					controller.Action = new ActionData(peekingReaction.Target, ActionFlag.Push, 0, 0, peekingReaction.TargetPosition, playerInteractable.Range, playerInteractable.Time, playerInteractable.Cost);

					// note: isActing tag component will stay on
				}
			}
		}
	}
}