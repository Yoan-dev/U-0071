using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	// note to reader: this is an attempt of a room-based partitioning system (see Room.cs for the components/singleton)
	// the goal is to ease detection by only iterating the elements in the same room of the enquiring process
	// at the beginning of the frame, objects that move or change will queue an update for the related room entities (addition, deletion, cache update)
	// rooms can be queried afterwards in order to retrieve their elements
	// room elements can also be modified outside of this system (example in ActionSystem.cs)

	// TODO: update before everyone except game init
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	public partial struct RoomSystem : ISystem, ISystemStartStop
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

		private ComponentLookup<RoomComponent> _roomLookup;
		private NativeParallelMultiHashMap<Entity, RoomUpdateEvent> _updates;
		private EntityQuery _query;
		private EntityQuery _deviceQuery;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Partition>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<PartitionComponent>()
				.WithAllRW<PositionComponent, InteractableComponent>()
				.WithNone<PickableComponent, DeviceTag>()
				.Build();

			_deviceQuery = SystemAPI.QueryBuilder()
				.WithAllRW<PartitionComponent, InteractableComponent>()
				.WithAll<PositionComponent, DeviceTag>()
				.Build();

			_updates = new NativeParallelMultiHashMap<Entity, RoomUpdateEvent>(0, Allocator.Persistent);
			_roomLookup = state.GetComponentLookup<RoomComponent>(true);
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_updates.Dispose();
		}

		[BurstCompile]
		public void OnStartRunning(ref SystemState state)
		{
		}

		[BurstCompile]
		public void OnStopRunning(ref SystemState state)
		{
			if (_updates.IsCreated)
			{
				_updates.Clear();
			}
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			Partition partition = SystemAPI.GetSingleton<Partition>();

			_roomLookup.Update(ref state);

			// map should be able to receive an add and leave event per moving entity and one per device
			int count = _query.CalculateEntityCount() * 2 + _deviceQuery.CalculateEntityCount();
			if (_updates.Capacity < count)
			{
				_updates.Capacity = count;
			}
			_updates.Clear();

			state.Dependency = new RoomInitJob().ScheduleParallel(state.Dependency);

			state.Dependency = new RoomUpdateJob
			{
				Partition = partition,
				Updates = _updates.AsParallelWriter(),
			}.ScheduleParallel(_query, state.Dependency);

			state.Dependency = new RoomDeviceUpdateJob
			{
				Partition = partition,
				Updates = _updates.AsParallelWriter(),
			}.ScheduleParallel(_deviceQuery, state.Dependency);

			state.Dependency = new PartitionUpdateJob
			{
				Updates = _updates,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new WorkProviderJob
			{
				RoomLookup = _roomLookup,
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
				else if (position.HasMoved || interactable.Changed)
				{
					// consume
					position.LastPosition = position.Value;
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
		public partial struct RoomDeviceUpdateJob : IJobEntity
		{
			[ReadOnly]
			public Partition Partition;
			[WriteOnly]
			public NativeParallelMultiHashMap<Entity, RoomUpdateEvent>.ParallelWriter Updates;

			public void Execute(Entity entity, in PositionComponent position, ref PartitionComponent partition, ref InteractableComponent interactable)
			{
				if (partition.CurrentRoom == Entity.Null)
				{
					Entity room = Partition.GetRoomData(position.Value).Entity;
					Updates.Add(room, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity, position.Value, in interactable),
						Type = RoomUpdateType.Addition,
					});
					partition.CurrentRoom = room;
				}
				else if (interactable.Changed)
				{
					// consume
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

			public void Execute(Entity entity, ref RoomComponent room, ref DynamicBuffer<RoomElementBufferElement> elements)
			{
				bool dirtyPopulation = false;

				if (Updates.TryGetFirstValue(entity, out RoomUpdateEvent update, out var it))
				{
					do
					{
						// TODO: keep an eye on perf (deletion/update)
						if (update.Type == RoomUpdateType.Deletion)
						{
							dirtyPopulation = true;
							RoomElementBufferElement.RemoveElement(ref elements, in update.Element);
						}
						else if (update.Type == RoomUpdateType.Update)
						{
							RoomElementBufferElement.UpdateElement(ref elements, in update.Element);
						}
						else // addition
						{
							dirtyPopulation = true;
							elements.Add(update.Element);
						}
					}
					while (Updates.TryGetNextValue(out update, ref it));
				}

				if (dirtyPopulation || room.Population > room.Capacity)
				{
					Entity lowest = Entity.Null;

					// recount all (safer because of death)
					room.Population = 0;
					using (var element = elements.GetEnumerator())
					{
						while (element.MoveNext())
						{
							// push check is the dirty way to check for characters
							if (element.Current.HasActionFlag(ActionFlag.Push))
							{
								lowest = lowest == Entity.Null || element.Current.Entity.Index < lowest.Index ? element.Current.Entity : lowest;
								room.Population++;
							}
						}
					}

					if (room.Population > room.Capacity)
					{
						room.FiredWorker = lowest;
					}
				}
			}
		}

		[BurstCompile]
		public partial struct WorkProviderJob : IJobEntity
		{
			[ReadOnly]
			public ComponentLookup<RoomComponent> RoomLookup;

			public void Execute(ref WorkInfoComponent info)
			{
				// reset
				info.LevelOneOpportunityCount = 0;
				info.LevelTwoOpportunityCount = 0;
				info.LevelThreeOpportunityCount = 0;
				info.AdminOpportunityCount = 0;

				// get new info
				ProcessRoom(info.Room1, ref info);
				ProcessRoom(info.Room2, ref info);
			}

			private void ProcessRoom(Entity entity, ref WorkInfoComponent info)
			{
				if (entity != Entity.Null)
				{
					RoomComponent room = RoomLookup[entity];
					if (room.Capacity > 0 && room.Capacity > room.Population)
					{
						int opportunityCount = room.Capacity - room.Population;

						if (room.Area == AreaAuthorization.LevelOne) info.LevelOneOpportunityCount += opportunityCount;
						else if (room.Area == AreaAuthorization.LevelTwo) info.LevelTwoOpportunityCount += opportunityCount;
						else if (room.Area == AreaAuthorization.LevelThree) info.LevelThreeOpportunityCount += opportunityCount;
						else if (room.Area == AreaAuthorization.Admin) info.AdminOpportunityCount += opportunityCount;
					}
				}
			}
		}

		[BurstCompile]
		public partial struct RoomInitJob : IJobEntity
		{
			public void Execute(ref RoomComponent room, in DynamicBuffer<RoomElementBufferElement> elements, EnabledRefRW<RoomInitTag> roomInit)
			{
				// wait for first partition update to run (get working devices)

				roomInit.ValueRW = false; // disable

				using (var element = elements.GetEnumerator())
				{
					while (element.MoveNext())
					{
						if (element.Current.Interactable.WorkingStationFlag)
						{
							room.Capacity++;
						}
					}
				}
			}
		}
	}
}