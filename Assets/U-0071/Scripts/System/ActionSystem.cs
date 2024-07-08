using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct ActionSystem : ISystem
	{
		private NativeQueue<ActionEvent> _eventQueue;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<PickComponent> _pickLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<PartitionComponent> _partitionLookup;
		private ComponentLookup<CreditsComponent> _creditsLookup;
		private ComponentLookup<SpawnerComponent> _spawnerLookup;

		public NativeQueue<ActionEvent>.ParallelWriter EventQueueWriter => _eventQueue.AsParallelWriter();

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<RoomPartition>();

			_eventQueue = new NativeQueue<ActionEvent>(Allocator.Persistent);

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>();
			_pickLookup = state.GetComponentLookup<PickComponent>();
			_pickableLookup = state.GetComponentLookup<PickableComponent>();
			_positionLookup = state.GetComponentLookup<PositionComponent>();
			_partitionLookup = state.GetComponentLookup<PartitionComponent>();
			_creditsLookup = state.GetComponentLookup<CreditsComponent>();
			_spawnerLookup = state.GetComponentLookup<SpawnerComponent>(true);
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_eventQueue.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			_roomElementLookup.Update(ref state);
			_pickLookup.Update(ref state);
			_pickableLookup.Update(ref state);
			_positionLookup.Update(ref state);
			_partitionLookup.Update(ref state);
			_creditsLookup.Update(ref state);
			_spawnerLookup.Update(ref state);

			// TODO: have generic events (destroyed, modifyCredits etc) written when processing actions and processed afterwards in // (avoid Lookup-fest)

			state.Dependency = new ActionEventsJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged),
				Partition = SystemAPI.GetSingleton<RoomPartition>(),
				Events = _eventQueue,
				RoomElementLookup = _roomElementLookup,
				PickLookup = _pickLookup,
				PickableLookup = _pickableLookup,
				PositionLookup = _positionLookup,
				PartitionLookup = _partitionLookup,
				CreditsLookup = _creditsLookup,
				SpawnerLookup = _spawnerLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct ActionEventsJob : IJob
		{
			public EntityCommandBuffer Ecb;
			public NativeQueue<ActionEvent> Events;
			public BufferLookup<RoomElementBufferElement> RoomElementLookup;
			public ComponentLookup<PickComponent> PickLookup;
			public ComponentLookup<PickableComponent> PickableLookup;
			public ComponentLookup<PositionComponent> PositionLookup;
			public ComponentLookup<PartitionComponent> PartitionLookup;
			public ComponentLookup<CreditsComponent> CreditsLookup;
			[ReadOnly]
			public ComponentLookup<SpawnerComponent> SpawnerLookup;
			[ReadOnly]
			public RoomPartition Partition;

			public void Execute()
			{
				NativeArray<ActionEvent> events = Events.ToArray(Allocator.Temp);
				using (var enumerator = events.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						ActionEvent actionEvent = enumerator.Current;

						// could be changed depending on action
						int cost = actionEvent.Action.Cost;

						if (actionEvent.Type == ActionType.Buy)
						{
							SpawnerComponent spawner = SpawnerLookup[actionEvent.Target];
							Ecb.SetComponent(Ecb.Instantiate(spawner.Prefab), new PositionComponent
							{
								Value = actionEvent.Action.Position + spawner.Offset,
								BaseYOffset = Const.ItemYOffset,
						});
						}
						else if (actionEvent.Type == ActionType.Trash)
						{
							ref PickComponent pick = ref PickLookup.GetRefRW(actionEvent.Source).ValueRW;
							Ecb.DestroyEntity(pick.Picked);
							pick.Picked = Entity.Null;
							PickLookup.SetComponentEnabled(actionEvent.Source, false);
						}
						else if (actionEvent.Type == ActionType.Pick)
						{
							// verify target has not been picked by another event
							if (PickableLookup.IsComponentEnabled(actionEvent.Target))
							{
								continue;
							}

							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = actionEvent.Target;
							PickLookup.SetComponentEnabled(actionEvent.Source, true);
							PickableLookup.GetRefRW(actionEvent.Target).ValueRW.Carrier = actionEvent.Source;
							PickableLookup.SetComponentEnabled(actionEvent.Target, true);

							Entity room = Partition.GetRoomData(actionEvent.Position).Entity;
							if (room != Entity.Null)
							{
								DynamicBuffer<RoomElementBufferElement> roomElements = RoomElementLookup[room];
								RoomElementBufferElement.RemoveElement(ref roomElements, new RoomElementBufferElement(actionEvent.Target));
								PartitionLookup.GetRefRW(actionEvent.Target).ValueRW.CurrentRoom = Entity.Null;
							}
						}
						else if (actionEvent.Type == ActionType.Drop)
						{
							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = Entity.Null;
							PickLookup.SetComponentEnabled(actionEvent.Source, false);
							PickableLookup.GetRefRW(actionEvent.Target).ValueRW.Carrier = Entity.Null;
							PickableLookup.SetComponentEnabled(actionEvent.Target, false);

							ref PositionComponent position = ref PositionLookup.GetRefRW(actionEvent.Target).ValueRW;
							position.Value = actionEvent.Action.Position;
							position.BaseYOffset = Const.ItemYOffset;
						}

						if (cost != 0f)
						{
							// can be negative (task reward)
							CreditsLookup.GetRefRW(actionEvent.Source).ValueRW.Value -= cost;
						}
					}
				}
				events.Dispose();
				Events.Clear();
			}
		}
	}
}