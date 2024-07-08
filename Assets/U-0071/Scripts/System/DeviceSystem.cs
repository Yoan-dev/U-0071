using Unity.Burst;
using Unity.Entities;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(ActionSystem))]
	public partial struct DeviceSystem : ISystem
	{
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			state.Dependency = new GrowJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new AutoSpawnJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
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

		[BurstCompile]
		[WithAll(typeof(AutoSpawnTag))]
		public partial struct AutoSpawnJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;

			public void Execute([ChunkIndexInQuery] int chunkIndex, ref SpawnerComponent spawner, in PositionComponent position, ref InteractableComponent interactable)
			{
				if (spawner.VariantCapacity > 0)
				{
					spawner.VariantCapacity--;
					Ecb.SetComponent(chunkIndex, Ecb.Instantiate(chunkIndex, spawner.Prefab), new PositionComponent
					{
						Value = position.Value + spawner.Offset,
						BaseYOffset = Const.PickableYOffset,
					});
				}
				else if (spawner.Capacity > 0)
				{
					spawner.Capacity--;
					Ecb.SetComponent(chunkIndex, Ecb.Instantiate(chunkIndex, spawner.Prefab), new PositionComponent
					{
						Value = position.Value + spawner.Offset,
						BaseYOffset = Const.PickableYOffset,
					});
				}
			}
		}
	}
}