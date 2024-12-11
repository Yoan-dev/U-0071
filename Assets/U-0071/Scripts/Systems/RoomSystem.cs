using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	// the goal of this system is to ease detection by only iterating the elements in the same room of the enquiring process
	// at the beginning of the frame, objects that move or change will queue an update for the related room entities (addition, deletion, cache update)
	// rooms can be queried afterwards in order to retrieve their elements
	// room elements can also be modified outside of this system (example in ActionSystem.cs)
	// (see Room.cs for the components/singleton)

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

		private NativeParallelMultiHashMap<Entity, RoomUpdateEvent> _updates;
		private EntityQuery _movingEntityquery;
		private EntityQuery _deviceQuery;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Partition>();

			_movingEntityquery = SystemAPI.QueryBuilder()
				.WithAllRW<PartitionInfoComponent>()
				.WithAllRW<PositionComponent, InteractableComponent>()
				.WithNone<PickableComponent, DeviceTag>()
				.Build();

			_deviceQuery = SystemAPI.QueryBuilder()
				.WithAllRW<PartitionInfoComponent, InteractableComponent>()
				.WithAll<PositionComponent, DeviceTag>()
				.Build();

			_updates = new NativeParallelMultiHashMap<Entity, RoomUpdateEvent>(0, Allocator.Persistent);
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

			// map should be able to receive an add and leave event per moving entity and one per device
			int count = _movingEntityquery.CalculateEntityCount() * 2 + _deviceQuery.CalculateEntityCount();
			if (_updates.Capacity < count)
			{
				_updates.Capacity = count;
			}
			_updates.Clear();

			// queue add/remove/update events for moving entities
			state.Dependency = new RoomMovingEntityUpdateJob
			{
				Partition = partition,
				Updates = _updates.AsParallelWriter(),
			}.ScheduleParallel(_movingEntityquery, state.Dependency);

			// queue add/update events for static entities
			state.Dependency = new RoomDeviceUpdateJob
			{
				Partition = partition,
				Updates = _updates.AsParallelWriter(),
			}.ScheduleParallel(_deviceQuery, state.Dependency);

			// update rooms buffer
			state.Dependency = new PartitionUpdateJob
			{
				Updates = _updates,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new RoomInitJob().ScheduleParallel(state.Dependency);

			// update work info providers (hallways, for AI)
			state.Dependency = new WorkProviderJob
			{
				RoomLookup = SystemAPI.GetComponentLookup<RoomComponent>(true),
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		public partial struct RoomMovingEntityUpdateJob : IJobEntity
		{
			[ReadOnly] public Partition Partition;
			[WriteOnly] public NativeParallelMultiHashMap<Entity, RoomUpdateEvent>.ParallelWriter Updates;

			public void Execute(Entity entity, ref PartitionInfoComponent partitionInfo, ref PositionComponent position, ref InteractableComponent interactable)
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
					partitionInfo.ClosestEdgeX = newRoom.GetClosestEdgeX(position.x);
				}

				if (newRoom.Entity != partitionInfo.CurrentRoom)
				{
					Updates.Add(newRoom.Entity, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity, position.Value, in interactable),
						Type = RoomUpdateType.Addition,
					});
					Updates.Add(partitionInfo.CurrentRoom, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity),
						Type = RoomUpdateType.Deletion,
					});
					partitionInfo.CurrentRoom = newRoom.Entity;
				}
				else if (position.HasMoved || interactable.Changed)
				{
					// consume
					position.LastPosition = position.Value;
					interactable.Changed = false;

					Updates.Add(partitionInfo.CurrentRoom, new RoomUpdateEvent
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
			[ReadOnly] public Partition Partition;
			[WriteOnly] public NativeParallelMultiHashMap<Entity, RoomUpdateEvent>.ParallelWriter Updates;

			public void Execute(Entity entity, in PositionComponent position, ref PartitionInfoComponent partitionInfo, ref InteractableComponent interactable)
			{
				if (partitionInfo.CurrentRoom == Entity.Null)
				{
					Entity room = Partition.GetRoomData(position.Value).Entity;
					Updates.Add(room, new RoomUpdateEvent
					{
						Element = new RoomElementBufferElement(entity, position.Value, in interactable),
						Type = RoomUpdateType.Addition,
					});
					partitionInfo.CurrentRoom = room;
				}
				else if (interactable.Changed)
				{
					// consume
					interactable.Changed = false;

					Updates.Add(partitionInfo.CurrentRoom, new RoomUpdateEvent
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
			[ReadOnly] public NativeParallelMultiHashMap<Entity, RoomUpdateEvent> Updates;

			public void Execute(Entity entity, ref RoomComponent room, ref DynamicBuffer<RoomElementBufferElement> elements)
			{
				bool dirtyPopulation = false;

				if (Updates.TryGetFirstValue(entity, out RoomUpdateEvent update, out var it))
				{
					// process all updates for this room
					do
					{
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

				// update room population info
				if (dirtyPopulation || room.Population > room.Capacity)
				{
					Entity fired = Entity.Null;

					// recount all (safer because of death)
					room.Population = 0;
					for (int i = elements.Length - 1; i >= 0; i--)
					{
						RoomElementBufferElement element = elements[i];

						// checking for push action flag is the dirty way to check for characters
						if (element.HasActionFlag(ActionFlag.Push))
						{
							if (fired == Entity.Null)
							{
								fired = element.Entity;
							}
							room.Population++;
						}
					}

					if (room.Population > room.Capacity)
					{
						// workplace too crowded, fire someone
						room.FiredWorker = fired;
					}
				}
			}
		}

		[BurstCompile]
		public partial struct WorkProviderJob : IJobEntity
		{
			[ReadOnly] public ComponentLookup<RoomComponent> RoomLookup;

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

						if (Utilities.HasAuthorization(room.Authorization, AreaAuthorization.LevelOne)) info.LevelOneOpportunityCount += opportunityCount;
						else if (Utilities.HasAuthorization(room.Authorization, AreaAuthorization.LevelTwo)) info.LevelTwoOpportunityCount += opportunityCount;
						else if (Utilities.HasAuthorization(room.Authorization, AreaAuthorization.LevelThree)) info.LevelThreeOpportunityCount += opportunityCount;
						else if (Utilities.HasAuthorization(room.Authorization, AreaAuthorization.Admin)) info.AdminOpportunityCount += opportunityCount;
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