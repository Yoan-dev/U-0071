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
				// should have animations as ids and just queue an id
				// (with a blob asset)
				Animation animation =
					actionController.IsResolving && actionController.Action.Time > 0f ?
					actionController.Action.ActionFlag == ActionFlag.Push ? Config.CharacterPush :
					actionController.Action.ActionFlag == ActionFlag.SpreadDisease ? Config.CharacterSpreadDisease :
					Config.CharacterInteract :
					!movement.Input.Equals(float2.zero) && position.HasSlightlyMoved ?
					movement.IsRunning ? Config.CharacterFlee :
					Config.CharacterWalk :
					Config.CharacterIdle;
				if (!controller.IsPlaying(animation))
				{
					controller.StartAnimation(in animation);
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
				}
				index.Value = controller.Animation.StartIndex + controller.Frame;
			}
		}
	}
}