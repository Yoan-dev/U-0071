using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MovementSystem))]
	[UpdateAfter(typeof(HealthSystem))]
	public partial struct PeekSystem : ISystem
	{
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<AuthorizationComponent> _authorizationLookup;
		private ComponentLookup<ActionController> _actionLookup;
		private NativeQueue<PeekingInfoComponent> _peekingInfos;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();

			_peekingInfos = new NativeQueue<PeekingInfoComponent>(Allocator.Persistent);

			_positionLookup = state.GetComponentLookup<PositionComponent>(true);
			_authorizationLookup = state.GetComponentLookup<AuthorizationComponent>(true);
			_actionLookup = state.GetComponentLookup<ActionController>(true);
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_peekingInfos.Dispose();
		}
		
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_positionLookup.Update(ref state);
			_authorizationLookup.Update(ref state);
			_actionLookup.Update(ref state);

			state.Dependency = new PeekingJob
			{
				PlayerEntity = SystemAPI.GetSingletonEntity<PlayerController>(),
				PositionLookup = _positionLookup,
				AuthorizationLookup = _authorizationLookup,
				ActionLookup = _actionLookup,
				PeekingInfos = _peekingInfos.AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new GetPeekingInfoJob
			{
				PeekingInfos = _peekingInfos,
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
			public NativeQueue<PeekingInfoComponent>.ParallelWriter PeekingInfos;

			[BurstCompile]
			public void Execute(Entity entity, ref PeekingComponent peeking, in DoorComponent door, in PositionComponent position, in InteractableComponent interactable)
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
						peeking.Suspicion = 0f;
					}
					else if (peeking.StartedFlag && interactable.CurrentUser != Entity.Null)
					{
						// progress
						ActionController controller = ActionLookup[interactable.CurrentUser];
						int newDigitIndex = (int)(5 * controller.Timer / controller.Action.Time);
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
	}
}