using Unity.Burst;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(GameInitSystem))]
	[UpdateBefore(typeof(RoomSystem))]
	public partial struct CycleSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
			state.RequireForUpdate<CycleComponent>();
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			Config config = SystemAPI.GetSingleton<Config>();
			ref CycleComponent cycle = ref SystemAPI.GetSingletonRW<CycleComponent>().ValueRW;

			cycle.CycleTimer -= SystemAPI.Time.DeltaTime;
			cycle.CycleChanged = false;

			if (cycle.CycleTimer < 0f)
			{
				cycle.CycleChanged = true;
				cycle.CycleTimer = config.CycleDuration;
				cycle.CycleCounter++;

				Random random = new Random((config.Seed + cycle.CycleCounter * Const.CycleCounterSeedIndex) % uint.MaxValue + 1);
				cycle.LevelOneCode = random.NextInt(0, 10000);
				cycle.LevelTwoCode = random.NextInt(0, 10000);
				cycle.LevelThreeCode = random.NextInt(0, 10000);
				cycle.RedCode = random.NextInt(0, 10000);
				cycle.BlueCode = random.NextInt(0, 10000);
				cycle.YellowCode = random.NextInt(0, 10000);

				// discover LVL1 code for this cycle
				char[] digits = cycle.GetCode(AreaAuthorization.LevelOne).ToString("0000").ToCharArray();
				KnownCodes.Instance.UpdateKnownCode(new KnownCode
				{
					Cycle = cycle.CycleCounter,
					Authorization = AreaAuthorization.LevelOne,
					FirstDigit = int.Parse(digits[0].ToString()),
					SecondDigit = int.Parse(digits[1].ToString()),
					ThirdDigit = int.Parse(digits[2].ToString()),
					FourthDigit = int.Parse(digits[3].ToString()),
				});
			}
		}
	}
}