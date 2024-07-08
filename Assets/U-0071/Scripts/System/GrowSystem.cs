using Unity.Burst;
using Unity.Entities;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(ActionSystem))]
	public partial struct GrowSystem : ISystem
	{
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new GrowJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		public partial struct GrowJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref GrowComponent grow, ref InteractableComponent interactable, ref SpawnerComponent spawner, ref TextureArrayIndex index)
			{
				if (spawner.Capacity > 0) return; // already grown

				grow.Timer += DeltaTime;

				if (grow.Timer > grow.Time)
				{
					grow.Timer = 0f;
					spawner.Capacity = 1;
					interactable.Flags |= ActionType.Collect;
					interactable.Changed = true;
					index.Value = grow.StageCount - 1;
				}
				else
				{
					index.Value = (int)((grow.StageCount - 1) * grow.Timer / grow.Time);
				}
			}
		}
	}
}