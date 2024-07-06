using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	// TODO: update before everyone except game init
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	public partial struct RoomSystem : ISystem
	{
		public enum RoomUpdateType
		{
			Addition = 0,
			Deletion,
			Update
		}

		public struct RoomUpdateEvent
		{
			public float2 Position;
			public Entity Entity;
			public ActionType ActionType;
			public RoomUpdateType UpdateType;
		}

		private NativeParallelMultiHashMap<Entity, RoomUpdateEvent> _updates;
		private EntityQuery _query;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<RoomPartition>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<PartitionComponent>()
				.WithAll<PositionComponent, InteractableComponent>()
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
			int count = _query.CalculateEntityCount() * 2;
			if (_updates.Capacity < count)
			{
				_updates.Capacity = count;
			}
			_updates.Clear();

			state.Dependency = new RoomUpdateJob
			{
				Partition = partition,
				Updates = _updates.AsParallelWriter(),
			}.ScheduleParallel(_query, state.Dependency);

			state.Dependency = new PartitionUpdateJob
			{
				Updates = _updates,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		// TODO: filter by !PickedFlag (TBD)
		public partial struct RoomUpdateJob : IJobEntity
		{
			[ReadOnly]
			public RoomPartition Partition;
			[WriteOnly]
			public NativeParallelMultiHashMap<Entity, RoomUpdateEvent>.ParallelWriter Updates;

			public void Execute(Entity entity, ref PartitionComponent partition, in PositionComponent position, in InteractableComponent interactable)
			{
				// Entity.Null events will be ignored during partition update
				Entity newRoom = Partition.GetRoom(position.Value);
				if (newRoom != partition.CurrentRoom)
				{
					Updates.Add(newRoom, new RoomUpdateEvent
					{
						Entity = entity,
						Position = position.Value,
						ActionType = interactable.Type,
						UpdateType = RoomUpdateType.Addition,
					});
					Updates.Add(partition.CurrentRoom, new RoomUpdateEvent
					{
						Entity = entity,
						UpdateType = RoomUpdateType.Deletion,
					});
					partition.CurrentRoom = newRoom;
				}
				else if (position.MovedFlag)
				{
					Updates.Add(partition.CurrentRoom, new RoomUpdateEvent
					{
						Entity = entity,
						Position = position.Value,
						ActionType = interactable.Type,
						UpdateType = RoomUpdateType.Update,
					});
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
						// TODO: keep an eye on perf (deletion/update)
						// reverse search because moving entities have more chance to be at the back
						if (update.UpdateType == RoomUpdateType.Deletion)
						{
							for (int i = elements.Length - 1; i >= 0; i--)
							{
								if (elements[i].Element == update.Entity)
								{
									elements.RemoveAtSwapBack(i);
									break;
								}
							}
						}
						else if (update.UpdateType == RoomUpdateType.Update)
						{
							for (int i = elements.Length - 1; i >= 0; i--)
							{
								if (elements[i].Element == update.Entity)
								{
									elements[i] = new RoomElementBufferElement
									{
										Element = update.Entity,
										Position = update.Position,
										ActionType = update.ActionType,
									};
									break;
								}
							}
						}
						else // addition
						{
							elements.Add(new RoomElementBufferElement
							{
								Element = update.Entity,
								Position = update.Position,
								ActionType = update.ActionType,
							});
						}
					}
					while (Updates.TryGetNextValue(out update, ref it));
				}
			}
		}
	}
}