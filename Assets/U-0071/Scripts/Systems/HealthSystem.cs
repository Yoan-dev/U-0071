using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using System;

namespace U0071
{
	public struct ContaminationEvent
	{
		public Entity Target;
		public float Value;
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(ActionSystem))]
	public partial struct HealthSystem : ISystem
	{
		private NativeQueue<ContaminationEvent> _contaminationEvents;
		private EntityQuery _contaminationQuery;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();

			_contaminationEvents = new NativeQueue<ContaminationEvent>(Allocator.Persistent);

			_contaminationQuery = SystemAPI.QueryBuilder()
				.WithAll<ContaminateComponent, PositionComponent, PartitionInfoComponent>()
				.WithAny<ContinuousContaminationTag, SickComponent>()
				.Build();
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_contaminationEvents.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			float deltaTime = SystemAPI.Time.DeltaTime;

			state.Dependency = new ContaminationJob()
			{
				DeltaTime = deltaTime,
				ContaminationEvents = _contaminationEvents.AsParallelWriter(),
				RoomElementBufferLookup = SystemAPI.GetBufferLookup<RoomElementBufferElement>(true),
			}.ScheduleParallel(_contaminationQuery, state.Dependency);

			state.Dependency = new PickableContaminationJob
			{
				ContaminationEvents = _contaminationEvents.AsParallelWriter(),
				DeltaTime = deltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new ContaminationEventsJob()
			{
				ContaminationEvents = _contaminationEvents,
				ContaminationLevelLookup = SystemAPI.GetComponentLookup<ContaminationLevelComponent>(),
			}.Schedule(state.Dependency);

			state.Dependency = new SickJob
			{
				DeltaTime = deltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new HealthJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				DeltaTime = deltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new DeathJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				RoomElementBufferLookup = SystemAPI.GetBufferLookup<RoomElementBufferElement>(true),
				AILookup = SystemAPI.GetComponentLookup<AIController>(),
				Config = SystemAPI.GetSingleton<Config>(),
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		[WithNone(typeof(InvincibleTag))]
		public partial struct HealthJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			public float DeltaTime;

			public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref HungerComponent hunger, ref ContaminationLevelComponent contaminationLevel)
			{
				hunger.Value -= DeltaTime * Const.HungerDepleteRate;
				if (hunger.Value <= 0f)
				{
					Ecb.SetComponent(chunkIndex, entity, new DeathComponent { Context = DeathType.Hunger });
					Ecb.SetComponentEnabled<DeathComponent>(chunkIndex, entity, true);
				}
				if (!contaminationLevel.IsSick && contaminationLevel.Value >= Const.ContaminationSickTreshold)
				{
					contaminationLevel.IsSick = true;
					Ecb.SetComponent(chunkIndex, entity, new SickComponent { IsResolved = false, SpreadTimer = Const.VomitTickTime });
					Ecb.SetComponentEnabled<SickComponent>(chunkIndex, entity, true);
				}
				contaminationLevel.Value = math.max(0, contaminationLevel.Value - DeltaTime * Const.ContaminationLevelDepleteRate);
			}
		}

		[BurstCompile]
		public partial struct DeathJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			[ReadOnly] public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly] public ComponentLookup<AIController> AILookup;
			public Config Config;

			public void Execute(
				[ChunkIndexInQuery] int chunkIndex,
				Entity entity,
				ref DeathComponent death,
				ref MovementComponent movement,
				ref PositionComponent position,
				ref AnimationController animation,
				ref InteractableComponent interactable,
				ref ContaminateComponent contaminate,
				ref SkinColor skin,
				ref ShortHairColor shortHair,
				ref LongHairColor longHair,
				ref BeardColor beard,
				in CreditsComponent credits,
				in PilosityComponent pilosity,
				in PartitionInfoComponent partitionInfo,
				EnabledRefRW<ResolveDeathTag> resolveDeath)
			{
				resolveDeath.ValueRW = false;

				movement.Input = float2.zero;
				position.BaseYOffset = Const.PickableYOffset;
				Ecb.SetComponentEnabled<DeathComponent>(chunkIndex, entity, true);
				Ecb.AddComponent(chunkIndex, entity, new PickableComponent
				{
					CarriedZOffset = Const.CorpseCarriedOffsetZ,
				});
				Ecb.SetComponentEnabled<PickableComponent>(chunkIndex, entity, false);

				interactable.Changed = true;
				interactable.ActionFlags &= ~ActionFlag.Push;

				contaminate.Strength = Const.CorpseContaminationStrength;
				Ecb.AddComponent(chunkIndex, entity, new ContinuousContaminationTag());

				if (death.Context == DeathType.Crushed)
				{
					animation.StartAnimation(in Config.CharacterCrushed);
				}
				else if (death.Context == DeathType.CrushedFromBelow)
				{
					animation.StartAnimation(in Config.CharacterCrushedFromBelow);
				}
				else
				{
					animation.StartAnimation(in Config.CharacterDie);
					interactable.ActionFlags |= ActionFlag.Pick;
					interactable.ItemFlags |= ItemFlag.Trash;
					if (credits.Value > 0)
					{
						// TBD add regardless
						// (empty research for player)
						// (need AI to ignore)
						interactable.ActionFlags |= ActionFlag.Search;
					}
				}
				
				float4 deathColorOffset = new float4(skin.Value.x * Const.DeathSkinMultiplier, skin.Value.y * Const.DeathSkinMultiplier, skin.Value.z * Const.DeathSkinMultiplier, 0f);
				if (!pilosity.HasShortHair) shortHair.Value += deathColorOffset;
				if (!pilosity.HasLongHair) longHair.Value += deathColorOffset;
				if (!pilosity.HasBeard) beard.Value += deathColorOffset;
				skin.Value += deathColorOffset;

				// trigger flee behavior
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partitionInfo.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						// push check will filter all items and devices
						if (enumerator.Current.Entity != entity && enumerator.Current.HasActionFlag(ActionFlag.Push) && AILookup.HasComponent(enumerator.Current.Entity))
						{
							AIController aiController = AILookup[enumerator.Current.Entity];
							aiController.Goal = AIGoal.Flee;
							aiController.ReassessmentTimer = Const.GetReassessmentTimer(AIGoal.Flee);
							Ecb.SetComponent(chunkIndex, enumerator.Current.Entity, aiController);
						}
					}
				}
			}
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		[WithNone(typeof(InvincibleTag))]
		public partial struct SickJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(
				ref SickComponent sick,
				ref ActionController controller,
				ref MovementComponent movement,
				ref SkinColor skin,
				ref ShortHairColor shortHair,
				ref LongHairColor longHair,
				ref BeardColor beard,
				ref HungerComponent hunger,
				ref ContaminationLevelComponent contaminationLevel,
				in PilosityComponent pilosity,
				EnabledRefRW<SickComponent> sickRef)
			{
				if (!sick.IsResolved)
				{
					sick.IsResolved = true;
					movement.Speed *= Const.SickSpeedMultiplier;

					float4 sickColorOffset = new float4(0f, skin.Value.y * Const.SickSkinGreenModifier, 0f, 0f);
					if (!pilosity.HasShortHair) shortHair.Value += sickColorOffset;
					if (!pilosity.HasLongHair) longHair.Value += sickColorOffset;
					if (!pilosity.HasBeard) beard.Value += sickColorOffset;
					skin.Value += sickColorOffset;
				}

				hunger.Value -= DeltaTime * Const.SickHungerDepleteRate;

				sick.SpreadTimer += DeltaTime;

				if (sick.SpreadTimer > Const.VomitTickTime)
				{
					sick.SpreadTimer -= Const.VomitTickTime;
					contaminationLevel.Value -= Const.ContaminationLevelVomitDecreaseValue;
					controller.Stop(true, true);
				}

				if (contaminationLevel.Value <= 0f)
				{
					contaminationLevel.IsSick = false;
					sick.SpreadTimer = 0;
					sickRef.ValueRW = false;
					sick.IsResolved = false;
					movement.Speed /= Const.SickSpeedMultiplier;

					float4 sickColorOffset = new float4(0f, skin.Value.y - (skin.Value.y / (1f + Const.SickSkinGreenModifier)), 0f, 0f);
					if (!pilosity.HasShortHair) shortHair.Value -= sickColorOffset;
					if (!pilosity.HasLongHair) longHair.Value -= sickColorOffset;
					if (!pilosity.HasBeard) beard.Value -= sickColorOffset;
					skin.Value -= sickColorOffset;
				}
			}
		}

		[BurstCompile]
		[WithNone(typeof(PickableComponent))]
		public partial struct ContaminationJob : IJobEntity
		{
			public NativeQueue<ContaminationEvent>.ParallelWriter ContaminationEvents;
			[ReadOnly] public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			public float DeltaTime;

			public void Execute(Entity entity, in ContaminateComponent contaminate, in PartitionInfoComponent partitionInfo, in PositionComponent position)
			{
				if (partitionInfo.CurrentRoom == Entity.Null)
				{
					// continuous contaminer that are carried
					return;
				}
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partitionInfo.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						// check for push action (living characters)
						if (enumerator.Current.Entity != entity &&
							enumerator.Current.HasActionFlag(ActionFlag.Push) &&
							position.IsInRange(enumerator.Current.Position, Const.ContaminationRange))
						{
							ContaminationEvents.Enqueue(new ContaminationEvent
							{
								Value = contaminate.Strength * DeltaTime,
								Target = enumerator.Current.Entity,
							});
						}
					}
				}
			}
		}

		[BurstCompile]
		[WithAll(typeof(PickableComponent))]
		public partial struct PickableContaminationJob : IJobEntity
		{
			public NativeQueue<ContaminationEvent>.ParallelWriter ContaminationEvents;
			public float DeltaTime;

			public void Execute(in ContaminateComponent contaminate, in PickableComponent piackable)
			{
				ContaminationEvents.Enqueue(new ContaminationEvent
				{
					Value = contaminate.Strength * DeltaTime,
					Target = piackable.Carrier,
				});
			}
		}

		[BurstCompile]
		public partial struct ContaminationEventsJob : IJob
		{
			public NativeQueue<ContaminationEvent> ContaminationEvents;
			public ComponentLookup<ContaminationLevelComponent> ContaminationLevelLookup;

			public void Execute()
			{
				while (ContaminationEvents.Count > 0)
				{
					ContaminationEvent contaminationEvent = ContaminationEvents.Dequeue();
					ref ContaminationLevelComponent contaminationLevel = ref ContaminationLevelLookup.GetRefRW(contaminationEvent.Target).ValueRW;

					contaminationLevel.Value = math.min(Const.MaxContaminationLevel, contaminationEvent.Value + contaminationLevel.Value);
				}
			}
		}
	}
}