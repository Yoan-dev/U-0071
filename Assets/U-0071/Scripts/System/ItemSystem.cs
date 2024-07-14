using Unity.Burst;
using Unity.Entities;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(ActionSystem))]
	[UpdateBefore(typeof(HealthSystem))]
	public partial struct ItemSystem : ISystem
	{
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			state.Dependency = new IgnoredItemJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		public partial struct IgnoredItemJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			public float DeltaTime;

			public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref IgnoredComponent ignored, ref InteractableComponent interactable)
			{
				ignored.Timer -= DeltaTime;
				if (ignored.Timer <= 0f)
				{
					interactable.IsIgnored = false;
					interactable.Changed = true;
					Ecb.RemoveComponent<IgnoredComponent>(chunkIndex, entity);
				}
			}
		}
	}
}