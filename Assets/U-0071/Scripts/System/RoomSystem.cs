using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace U0071
{
	// TODO: update before everyone except game init
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	public partial struct RoomSystem : ISystem
	{
		public struct RoomUpdateEvent
		{
			public Entity Entity;
			public bool Addition;
		}

		private NativeParallelMultiHashMap<Entity, RoomUpdateEvent> _updates;
		private EntityQuery _partitionQuery;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<RoomPartition>();

			_partitionQuery = SystemAPI.QueryBuilder()
				.WithAllRW<PartitionComponent>()
				.WithAll<PositionComponent>()
				.Build();

			_updates = new NativeParallelMultiHashMap<Entity, RoomUpdateEvent>(0, Allocator.Persistent);
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_updates.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// force dependency for other systems
			// (native collection RW)
			ref RoomPartition partition = ref SystemAPI.GetSingletonRW<RoomPartition>().ValueRW;

			// map should be able to receive an add and leave event for each paritioned entity
			int count = _partitionQuery.CalculateEntityCount() * 2;
			if (_updates.Capacity < count)
			{
				_updates.Capacity = count;
			}
			_updates.Clear();

			state.Dependency = new RoomUpdateJob
			{
				Partition = partition,
				Updates = _updates.AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new PartitionUpdateJob
			{
				Updates = _updates,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		// TODO: filter by MovedFlag (need to be true on first frame)
		public partial struct RoomUpdateJob : IJobEntity
		{
			[ReadOnly]
			public RoomPartition Partition;
			[WriteOnly]
			public NativeParallelMultiHashMap<Entity, RoomUpdateEvent>.ParallelWriter Updates;

			public void Execute(Entity entity, ref PartitionComponent partition, in PositionComponent position)
			{
				Entity newRoom = Partition.GetRoom(position.Value);
				if (newRoom != partition.CurrentRoom)
				{
					// Entity.Null events will be ignored during partition update
					Updates.Add(newRoom, new RoomUpdateEvent
					{
						Entity = entity,
						Addition = true,
					});
					Updates.Add(partition.CurrentRoom, new RoomUpdateEvent
					{
						Entity = entity,
						Addition = false,
					});
					partition.CurrentRoom = newRoom;
				}
			}
		}

		[BurstCompile]
		public partial struct PartitionUpdateJob : IJobEntity
		{
			[ReadOnly]
			public NativeParallelMultiHashMap<Entity, RoomUpdateEvent> Updates;

			public void Execute(Entity entity, ref DynamicBuffer<RoomElementBufferElement> elements)
			{
				if (Updates.TryGetFirstValue(entity, out RoomUpdateEvent update, out var it))
				{
					do
					{
						if (update.Addition)
						{
							elements.Add(new RoomElementBufferElement { Element = update.Entity });
						}
						else
						{
							// TODO: keep an eye on perf
							// reverse search because moving entities have more chance to be at the back
							for (int i = elements.Length - 1; i >= 0; i--)
							{
								if (elements[i].Element == update.Entity)
								{
									elements.RemoveAtSwapBack(i);
									break;
								}
							}
						}
					}
					while (Updates.TryGetNextValue(out update, ref it));
				}
			}
		}
	}
}