using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using static U0071.HealthSystem;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(ActionSystem))]
	[UpdateBefore(typeof(HealthSystem))]
	public partial struct DeviceSystem : ISystem
	{
		public BufferLookup<RoomElementBufferElement> _roomElementLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<RoomPartition>();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			_roomElementLookup.Update(ref state);

			state.Dependency = new GrowJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new AutoSpawnJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new HazardJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				RoomElementBufferLookup = _roomElementLookup,
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

		[BurstCompile]
		public partial struct HazardJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;

			public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in HazardComponent hazard, in PositionComponent position, in PartitionComponent partition)
			{
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						// check for push action (living characters)
						// TODO: a better way
						if (enumerator.Current.Entity != entity &&
							enumerator.Current.HasActionType(ActionType.Push) &&
							Utilities.IsInCircle(position.Value, enumerator.Current.Position, hazard.Range))
						{
							DeathComponent death = new DeathComponent { Context = hazard.DeathContext };
							Ecb.SetComponent(chunkIndex, enumerator.Current.Entity, death);
							Ecb.SetComponentEnabled<DeathComponent>(chunkIndex, enumerator.Current.Entity, true);
						}
					}
				}
			}
		}
	}
}