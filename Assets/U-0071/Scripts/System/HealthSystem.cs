using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(ActionSystem))]
	public partial struct HealthSystem : ISystem
	{
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<AIController> _aiLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_aiLookup = SystemAPI.GetComponentLookup<AIController>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_roomElementLookup.Update(ref state);
			_aiLookup.Update(ref state);

			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			float deltaTime = SystemAPI.Time.DeltaTime;

			state.Dependency = new SickJob
			{
				DeltaTime = deltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new HungerJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				DeltaTime = deltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new DeathJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				RoomElementBufferLookup = _roomElementLookup,
				AILookup = _aiLookup,
				Config = SystemAPI.GetSingleton<Config>(),
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		[WithNone(typeof(InvincibleTag))]
		public partial struct HungerJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			public float DeltaTime;

			public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref HungerComponent hunger)
			{
				hunger.Value -= DeltaTime * Const.HungerDepleteRate;
				if (hunger.Value <= 0f)
				{
					DeathComponent death = new DeathComponent { Context = DeathType.Hunger };
					Ecb.SetComponent(chunkIndex, entity, death);
					Ecb.SetComponentEnabled<DeathComponent>(chunkIndex, entity, true);
				}
			}
		}

		[BurstCompile]
		public partial struct DeathJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<AIController> AILookup;
			public Config Config;

			public void Execute(
				[ChunkIndexInQuery] int chunkIndex,
				Entity entity,
				ref DeathComponent death,
				ref MovementComponent movement,
				ref PositionComponent position,
				ref AnimationController animation,
				ref InteractableComponent interactable,
				ref SkinColor skin,
				ref ShortHairColor shortHair,
				ref LongHairColor longHair,
				ref BeardColor beard,
				in CreditsComponent credits,
				in PilosityComponent pilosity,
				in PartitionComponent partition,
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

				float4 deathColorOffset = new float4(Const.DeathSkinToneOffset, Const.DeathSkinToneOffset, Const.DeathSkinToneOffset, 0f);
				if (!pilosity.HasShortHair) shortHair.Value += deathColorOffset;
				if (!pilosity.HasLongHair) longHair.Value += deathColorOffset;
				if (!pilosity.HasBeard) beard.Value += deathColorOffset;
				skin.Value += deathColorOffset;

				// trigger flee behavior
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
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
				ref MovementComponent movement,
				ref SkinColor skin,
				ref ShortHairColor shortHair,
				ref LongHairColor longHair,
				ref BeardColor beard,
				ref HungerComponent hunger,
				in PilosityComponent pilosity,
				EnabledRefRW<SickComponent> sickRef)
			{
				if (!sick.IsResolved)
				{
					sick.IsResolved = true;
					movement.Speed *= Const.SickSpeedMultiplier;

					float4 sickColorOffset = new float4(0f, Const.SickSkinToneOffset, 0f, 0f);
					if (!pilosity.HasShortHair) shortHair.Value += sickColorOffset;
					if (!pilosity.HasLongHair) longHair.Value += sickColorOffset;
					if (!pilosity.HasBeard) beard.Value += sickColorOffset;
					skin.Value += sickColorOffset;
				}

				hunger.Value -= DeltaTime * Const.SickHungerDepleteRate;

				sick.Timer += DeltaTime;
				if (sick.Timer > Const.SickTime)
				{
					sickRef.ValueRW = false;
					sick.IsResolved = false;
					movement.Speed /= Const.SickSpeedMultiplier;

					float4 sickColorOffset = new float4(0f, Const.SickSkinToneOffset, 0f, 0f);
					if (!pilosity.HasShortHair) shortHair.Value -= sickColorOffset;
					if (!pilosity.HasLongHair) longHair.Value -= sickColorOffset;
					if (!pilosity.HasBeard) beard.Value -= sickColorOffset;
					skin.Value -= sickColorOffset;
				}
			}
		}
	}
}