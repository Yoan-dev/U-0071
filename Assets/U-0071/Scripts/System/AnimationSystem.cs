using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial struct AnimationSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new CharacterAnimationJob
			{
				Config = SystemAPI.GetSingleton<Config>(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new AnimationJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(SimpleAnimationTag))]
		[WithNone(typeof(DeathComponent))]
		public partial struct CharacterAnimationJob : IJobEntity
		{
			public Config Config;

			public void Execute(ref AnimationController controller, in MovementComponent movement, in PositionComponent position, in ActionController actionController)
			{
				if (actionController.IsResolving)
				{
					if (!controller.IsPlaying(in Config.CharacterInteract))
					{
						controller.StartAnimation(in Config.CharacterInteract);
					}
				}
				else if (!movement.Input.Equals(float2.zero) && position.HasSlightlyMoved)
				{
					if (movement.IsRunning)
					{
						if (!controller.IsPlaying(in Config.CharacterFlee))
						{
							controller.StartAnimation(in Config.CharacterFlee);
						}
					}
					else if (!controller.IsPlaying(in Config.CharacterWalk))
					{
						controller.StartAnimation(in Config.CharacterWalk);
					}
				}
				else if (!controller.IsPlaying(in Config.CharacterIdle))
				{
					controller.StartAnimation(in Config.CharacterIdle);
				}
			}
		}

		[BurstCompile]
		public partial struct AnimationJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref AnimationController controller, ref TextureArrayIndex index)
			{
				controller.Timer += DeltaTime;
				if (controller.Timer > controller.Animation.FrameTime && (controller.Animation.Looping || controller.Frame < controller.Animation.Duration - 1))
				{
					controller.Timer -= controller.Animation.FrameTime;
					controller.Frame = (controller.Frame + 1) % controller.Animation.Duration;
					index.Value = controller.Animation.StartIndex + controller.Frame;
				}
			}
		}
	}
}