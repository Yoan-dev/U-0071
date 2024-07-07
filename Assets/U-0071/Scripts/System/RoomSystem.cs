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
			public RoomElementBufferElement Element;
			public RoomUpdateType Type;
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
				.WithNone<PickedComponent>()
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
			// map should be able to receive an add and leave event for each paritioned entity
			int count = _query.CalculateEntityCount() * 2;
			if (_updates.Capacity < count)
			{
				_updates.Capacity = count;
			}
			_updates.Clear();

			state.Dependency = new RoomUpdateJob
			{
				Partition = SystemAPI.GetSingleton<RoomPartition>(),
				Updates = _updates.AsParallelWriter(),
			}.ScheduleParallel(_query, state.Dependency);

			state.Dependency = new PartitionUpdateJob
			{
				Updates = _updates,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
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
						Element = new RoomElementBufferElement(entity, position.Value, interactable.Type),
						Type = RoomUpdateType.Addition,
					});
					Updates.Add(partition.CurrentRoom, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity),
						Type = RoomUpdateType.Deletion,
					});
					partition.CurrentRoom = newRoom;
				}
				else if (position.MovedFlag)
				{
					Updates.Add(partition.CurrentRoom, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity, position.Value, interactable.Type),
						Type = RoomUpdateType.Update,
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
						if (update.Type == RoomUpdateType.Deletion)
						{
							RoomElementBufferElement.RemoveElement(ref elements, in update.Element);
						}
						else if (update.Type == RoomUpdateType.Update)
						{
							RoomElementBufferElement.UpdateElement(ref elements, in update.Element);
						}
						else // addition
						{
							elements.Add(update.Element);
						}
					}
					while (Updates.TryGetNextValue(out update, ref it));
				}
			}
		}
	}
}