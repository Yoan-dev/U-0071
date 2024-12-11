using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	[UpdateAfter(typeof(AnimationSystem))]
	public partial struct RandomEffectsSystem : ISystem, ISystemStartStop
	{
		private float _startTimer;
		private float _tickTimer;
		private bool _started;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
			state.RequireForUpdate<PlayerController>();
		}

		public void OnStartRunning(ref SystemState state)
		{
			_startTimer = 0f;
			_tickTimer = 0f;
			_started = false;
		}

		public void OnStopRunning(ref SystemState state)
		{
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// gives random colors and animations to units on player death
			// (simulation going wild)

			if (state.EntityManager.IsComponentEnabled<DeathComponent>(SystemAPI.GetSingletonEntity<PlayerController>()))
			{
				if (_started)
				{
					_tickTimer += SystemAPI.Time.DeltaTime;
					bool shouldUpdate = _tickTimer > Const.UnitRandomEffectsTime;

					state.Dependency = new UnitRandomEffectsJob()
					{
						Config = SystemAPI.GetSingleton<Config>(),
						ShouldUpdate = shouldUpdate,
					}.ScheduleParallel(state.Dependency);

					if (shouldUpdate)
					{
						_tickTimer -= Const.UnitRandomEffectsTime;
					}
				}
				else
				{
					_startTimer += SystemAPI.Time.DeltaTime;
					if (_startTimer > Const.UnitRandomEffectsStartTime)
					{
						_started = true;
					}
				}
			}
		}

		[BurstCompile]
		[WithNone(typeof(SimpleAnimationTag))]
		[WithNone(typeof(DeathComponent))]
		public partial struct UnitRandomEffectsJob : IJobEntity
		{
			public Config Config;
			public bool ShouldUpdate;

			public void Execute(
				ref TextureArrayIndex textureArray,
				ref SkinColor skin,
				ref ShortHairColor shortHair,
				ref LongHairColor longHair,
				ref BeardColor beard,
				ref ShirtColor shirt)
			{
				textureArray.Value = (skin.Value.x + shirt.Value.y) * Config.UnitArrayMaxIndex % Config.UnitArrayMaxIndex;
				if (ShouldUpdate)
				{
					skin.Value = new float4(shortHair.Value.y, longHair.Value.z, beard.Value.x, 1f);
					shortHair.Value = new float4(skin.Value.y, shirt.Value.x, shortHair.Value.z, 1f);
					longHair.Value = new float4(longHair.Value.x, beard.Value.y, skin.Value.z, 1f);
					beard.Value = new float4(shirt.Value.y, shortHair.Value.x, longHair.Value.y, 1f);
					shirt.Value = new float4(beard.Value.z, skin.Value.x, shirt.Value.z, 1f);
				}
			}
		}
	}
}