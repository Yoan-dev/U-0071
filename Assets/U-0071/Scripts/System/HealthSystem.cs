using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(ActionSystem))]
	public partial struct HealthSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			state.Dependency = new HungerJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				Config = SystemAPI.GetSingleton<Config>(),
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new PushedJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(IsDeadTag))]
		public partial struct HungerJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			public Config Config;
			public float DeltaTime;

			public void Execute(
				[ChunkIndexInQuery] int chunkIndex, 
				Entity entity, 
				ref HungerComponent hunger, 
				ref MovementComponent movement, 
				ref ActionController controller,
				ref AnimationController animation,
				ref InteractableComponent interactable,
				ref SkinColor skin,
				ref ShortHairColor shortHair,
				ref LongHairColor longHair,
				ref BeardColor beard,
				in CreditsComponent credits)
			{
				hunger.Value -= DeltaTime * Const.HungerDepleteRate;
				if (hunger.Value <= 0f)
				{
					movement.Input = float2.zero;
					controller.Stop();
					Ecb.SetComponentEnabled<IsActing>(chunkIndex, entity, false);
					Ecb.SetComponentEnabled<IsDeadTag>(chunkIndex, entity, true);
					Ecb.AddComponent(chunkIndex, entity, new PickableComponent
					{
						CarriedZOffset = Const.CorpseCarriedOffsetZ,
					});
					Ecb.SetComponentEnabled<PickableComponent>(chunkIndex, entity, false);
					animation.StartAnimation(in Config.CharacterDie);
					interactable.Flags |= ActionType.Pick;
					interactable.Flags |= ActionType.RefTrash;
					interactable.Flags &= ~ActionType.Push;
					if (credits.Value > 0)
					{
						interactable.Flags |= ActionType.Search;
					}
					interactable.Changed = true;

					float4 deathColorOffset = new float4(Const.DeathSkinToneOffset, Const.DeathSkinToneOffset, Const.DeathSkinToneOffset, 0f);
					shortHair.Value += deathColorOffset;
					longHair.Value += deathColorOffset;
					beard.Value += deathColorOffset;
					skin.Value += deathColorOffset;
				}
			}
		}

		[BurstCompile]
		[WithNone(typeof(IsDeadTag))]
		public partial struct PushedJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref PushedComponent pushed, ref PositionComponent position, EnabledRefRW<PushedComponent> pushedRef)
			{
				// TODO: if time
				//if (!animation.IsPlaying(Config.CharacterPushed))
				//{
				//	animation.StartAnimation(Config.CharacterPushed);
				//}

				position.Value += pushed.Direction * Const.PushedSpeed * DeltaTime;
				position.MovedFlag = true;

				pushed.Timer -= DeltaTime;
				if (pushed.Timer <= 0f)
				{
					pushedRef.ValueRW = false;
				}
			}
		}
	}
}