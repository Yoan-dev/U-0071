using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	// note to reader: this is an attempt of a room-based partitioning system (see Room.cs for the components/singleton)
	// the goal is to ease detection by only iterating the elements in the same room of the enquiring process
	// at the beginning of the frame, objects that move or change room will queue an update for the related room entities (addition, deletion, cache update)
	// rooms can be queried afterwards in order to retrieve their elements
	// room elements can also be modified outside of this system (example in ActionSystem.cs)

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
			state.RequireForUpdate<Partition>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<PartitionComponent>()
				.WithAll<PositionComponent, InteractableComponent>()
				.WithNone<PickableComponent>()
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
				Partition = SystemAPI.GetSingleton<Partition>(),
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
			public Partition Partition;
			[WriteOnly]
			public NativeParallelMultiHashMap<Entity, RoomUpdateEvent>.ParallelWriter Updates;

			public void Execute(Entity entity, ref PartitionComponent partition, ref PositionComponent position, ref InteractableComponent interactable)
			{
				// TODO: static entities should be initiated once on start and get filtered from this job
				// (except on interactable changed)

				// Entity.Null events will be ignored during partition update
				RoomData newRoom = Partition.GetRoomData(position.Value);

				if (newRoom.Entity != Entity.Null)
				{
					// update Y offset (for sorting) depending on position ratio in the room
					float2 roomRatio = newRoom.GetRoomRatio(position.Value);
					position.CurrentYOffset = position.BaseYOffset +
						roomRatio.x * Const.YOffsetRatioX +
						roomRatio.y * Const.YOffsetRatioY;
				}

				if (newRoom.Entity != partition.CurrentRoom)
				{
					Updates.Add(newRoom.Entity, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity, position.Value, in interactable),
						Type = RoomUpdateType.Addition,
					});
					Updates.Add(partition.CurrentRoom, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity),
						Type = RoomUpdateType.Deletion,
					});
					partition.CurrentRoom = newRoom.Entity;
				}
				else if (position.MovedFlag || interactable.Changed)
				{
					// consume
					position.MovedFlag = false;
					interactable.Changed = false;

					Updates.Add(partition.CurrentRoom, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity, position.Value, in interactable),
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