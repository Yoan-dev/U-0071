using TMPro;
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
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Partition>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

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
				RoomElementBufferLookup = SystemAPI.GetBufferLookup<RoomElementBufferElement>(true),
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		public partial struct GrowJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref GrowComponent grow, ref InteractableComponent interactable, ref SpawnerComponent spawner, ref TextureArrayIndex index)
			{
				if (spawner.Capacity + spawner.VariantCapacity > 0)
				{
					// already grown
					if (grow.SpawnVariantFlag)
					{
						// replace
						spawner.VariantCapacity = 1;
						spawner.Capacity = 0;
						grow.SpawnVariantFlag = false; // consume flag
					}
					return;
				}

				grow.Timer += DeltaTime;

				if (grow.Timer > grow.Time)
				{
					grow.Timer = 0f;
					if (grow.SpawnVariantFlag)
					{
						grow.SpawnVariantFlag = false; // consume flag
						spawner.VariantCapacity = 1;
					}
					else
					{
						spawner.Capacity = 1;
					}
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
				index.Value = math.clamp(spawner.Capacity + spawner.VariantCapacity, 0, capacityFeedback.StageCount - 1);
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

			public void Execute(ref DoorComponent door, ref InteractableComponent interactable, ref TextureArrayIndex textureArrayIndex, EnabledRefRW<DoorComponent> doorRef)
			{
				door.OpenTimer += DeltaTime;

				if (door.OpenTimer >= door.StaysOpenTime)
				{
					door.OpenTimer = 0f;
					doorRef.ValueRW = false;
					textureArrayIndex.Value = 0f;
					interactable.ActionFlags |= ActionFlag.Open;
					interactable.Changed = true;
				}
				else
				{
					float halfTime = door.StaysOpenTime / 2f;
					float value = door.OpenTimer <= halfTime ?
						door.OpenTimer / halfTime :
						1f - (door.OpenTimer - halfTime) / halfTime;

					// plateau
					textureArrayIndex.Value = (door.StageCount - 1) * math.clamp(Utilities.EaseOutCubic(value, door.AnimationCubicStrength), 0f, 1f);
				}
			}
		}

		[BurstCompile]
		public partial struct HazardJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			[ReadOnly] public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;

			public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in HazardComponent hazard, in PartitionInfoComponent partition , in BoundsComponent bounds, in PositionComponent position)
			{
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						// check for push action (living characters)
						if (enumerator.Current.Entity != entity &&
							enumerator.Current.HasActionFlag(ActionFlag.Push) &&
							Utilities.IsInBounds(enumerator.Current.Position, in bounds))
						{
							DeathType deathType = hazard.DeathType;
							if (deathType == DeathType.Crushed && position.Value.y < enumerator.Current.Position.y)
							{
								deathType = DeathType.CrushedFromBelow;
							}

							DeathComponent death = new DeathComponent { Context = deathType };
							Ecb.SetComponent(chunkIndex, enumerator.Current.Entity, death);
							Ecb.SetComponentEnabled<DeathComponent>(chunkIndex, enumerator.Current.Entity, true);
						}
					}
				}
			}
		}
	}
}