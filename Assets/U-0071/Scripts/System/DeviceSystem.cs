using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
			state.RequireForUpdate<Partition>();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			_roomElementLookup.Update(ref state);

			float deltaTime = SystemAPI.Time.DeltaTime;

			state.Dependency = new GrowJob
			{
				DeltaTime = deltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new CapacityFeedbackJob().ScheduleParallel(state.Dependency);

			state.Dependency = new AutoSpawnJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new AutoDestroyJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new DoorJob
			{
				DeltaTime = deltaTime,
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
					interactable.ActionFlags |= ActionFlag.Collect;
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
		public partial struct CapacityFeedbackJob : IJobEntity
		{
			public void Execute(in SpawnerComponent spawner, in CapacityFeedbackComponent capacityFeedback, ref TextureArrayIndex index)
			{
				index.Value = math.clamp(spawner.Capacity, 0, capacityFeedback.StageCount - 1);
			}
		}

		[BurstCompile]
		[WithAll(typeof(AutoSpawnTag))]
		public partial struct AutoSpawnJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;

			public void Execute([ChunkIndexInQuery] int chunkIndex, ref SpawnerComponent spawner, in PositionComponent position)
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
		[WithAll(typeof(AutoDestroyTag))]
		public partial struct AutoDestroyJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;

			public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in SpawnerComponent spawner)
			{
				if (spawner.Capacity == 0 && spawner.VariantCapacity == 0)
				{
					Ecb.DestroyEntity(chunkIndex, entity);
				}
			}
		}

		[BurstCompile]
		public partial struct DoorJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref DoorComponent door, ref InteractableComponent interactable, EnabledRefRW<DoorComponent> doorRef)
			{
				door.OpenTimer += DeltaTime;

				if (door.OpenTimer >= door.StaysOpenTime)
				{
					door.OpenTimer = 0f;
					doorRef.ValueRW = false;
					interactable.ActionFlags |= ActionFlag.Open;
					interactable.Changed = true;
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
							enumerator.Current.HasActionFlag(ActionFlag.Push) &&
							Utilities.IsInCircle(position.Value, enumerator.Current.Position, hazard.Range))
						{
							DeathComponent death = new DeathComponent { Context = hazard.DeathType };
							Ecb.SetComponent(chunkIndex, enumerator.Current.Entity, death);
							Ecb.SetComponentEnabled<DeathComponent>(chunkIndex, enumerator.Current.Entity, true);
						}
					}
				}
			}
		}
	}
}