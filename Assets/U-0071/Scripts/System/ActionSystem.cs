using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace U0071
{
	public struct ActionEvent
	{
		public ActionData Action;
		public Entity Source;

		public Entity Target => Action.Target;
		public ActionType Type => Action.Type;
		public float2 Position => Action.Position;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ActionEvent(Entity source, in ActionData action)
		{
			Source = source;
			Action = action;
		}
	}

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
		private ComponentLookup<InteractableComponent> _interactableLookup;
		private ComponentLookup<HungerComponent> _hungerLookup;
		private ComponentLookup<PushedComponent> _pushedLookup;
		private ComponentLookup<StorageComponent> _storageLookup;

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
			_spawnerLookup = state.GetComponentLookup<SpawnerComponent>();
			_interactableLookup = state.GetComponentLookup<InteractableComponent>();
			_hungerLookup = state.GetComponentLookup<HungerComponent>();
			_pushedLookup = state.GetComponentLookup<PushedComponent>();
			_storageLookup = state.GetComponentLookup<StorageComponent>(true);
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
			_interactableLookup.Update(ref state);
			_hungerLookup.Update(ref state);
			_storageLookup.Update(ref state);
			_pushedLookup.Update(ref state);

			// TODO: have generic events (destroyed, modifyCredits etc) written when processing actions and processed afterwards in // (avoid Lookup-fest)
			// TBD: use Ecb instead of Lookup-fest ?

			state.Dependency = new ActionUpdateJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
				Events = _eventQueue.AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

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
				InteractableLookup = _interactableLookup,
				HungerLookup = _hungerLookup,
				StorageLookup = _storageLookup,
				PushedLookup = _pushedLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct ActionUpdateJob : IJobEntity
		{
			[WriteOnly]
			public NativeQueue<ActionEvent>.ParallelWriter Events;
			public float DeltaTime;

			public void Execute(Entity entity, ref ActionController controller, in CreditsComponent credits, EnabledRefRW<IsActing> isActing)
			{
				// do not filter isDeadTag to be able to drop carried item on death
				// (isActing will filter the job after)

				controller.Timer += DeltaTime;
				if (controller.Timer >= controller.Action.Time)
				{
					if (controller.Action.Cost <= 0f || controller.Action.Cost <= credits.Value)
					{
						Events.Enqueue(new ActionEvent(entity, controller.Action));
					}
					controller.Stop();
					isActing.ValueRW = false;
				}
			}
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
			public ComponentLookup<SpawnerComponent> SpawnerLookup;
			public ComponentLookup<InteractableComponent> InteractableLookup;
			public ComponentLookup<HungerComponent> HungerLookup;
			public ComponentLookup<PushedComponent> PushedLookup;
			[ReadOnly]
			public ComponentLookup<StorageComponent> StorageLookup;
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

						if (actionEvent.Type == ActionType.Trash || actionEvent.Type == ActionType.Store || actionEvent.Type == ActionType.Eat)
						{
							// destroy used item
							ref PickComponent pick = ref PickLookup.GetRefRW(actionEvent.Source).ValueRW;
							Ecb.DestroyEntity(pick.Picked);
							pick.Picked = Entity.Null;
							pick.Flags = 0;
							pick.Time = 0f;
							PickLookup.SetComponentEnabled(actionEvent.Source, false);
						}

						if (actionEvent.Type == ActionType.Push)
						{
							ref PushedComponent pushed = ref PushedLookup.GetRefRW(actionEvent.Target).ValueRW;
							pushed.Direction = math.normalizesafe(actionEvent.Position - PositionLookup[actionEvent.Source].Value);
							pushed.Timer = Const.PushedTimer;
							PushedLookup.SetComponentEnabled(actionEvent.Target, true);
						}
						else if (actionEvent.Type == ActionType.Search)
						{
							ref CreditsComponent credits = ref CreditsLookup.GetRefRW(actionEvent.Target).ValueRW;
							int gain = math.min(math.max(0, credits.Value), Const.LootCreditsCount);
							cost -= gain;
							credits.Value -= gain;

							if (credits.Value <= 0f)
							{
								// remove action type
								ref InteractableComponent interactable = ref InteractableLookup.GetRefRW(actionEvent.Target).ValueRW;
								interactable.Flags &= ~ActionType.Search;
								interactable.Changed = true;
							}
						}
						else if (actionEvent.Type == ActionType.Eat)
						{
							HungerLookup.GetRefRW(actionEvent.Source).ValueRW.Value += Const.EatingHungerGain;
						}
						else if (actionEvent.Type == ActionType.Store)
						{
							StorageComponent storage = StorageLookup[actionEvent.Target];

							// TODO: increase variable capacity (if !refFlag ?)
							IncreaseCapacity(storage.Destination);
							if (storage.SecondaryDestination != Entity.Null)
							{
								IncreaseCapacity(storage.SecondaryDestination);
							}
						}
						else if (actionEvent.Type == ActionType.Collect)
						{
							ref SpawnerComponent spawner = ref SpawnerLookup.GetRefRW(actionEvent.Target).ValueRW;
							if (spawner.VariantCapacity > 0)
							{
								spawner.VariantCapacity--;
								Ecb.SetComponent(Ecb.Instantiate(spawner.VariantPrefab), new PositionComponent
								{
									Value = actionEvent.Action.Position + spawner.Offset,
									BaseYOffset = Const.PickableYOffset,
								});
							}
							else if (spawner.Capacity > 0)
							{
								spawner.Capacity--;
								Ecb.SetComponent(Ecb.Instantiate(spawner.Prefab), new PositionComponent
								{
									Value = actionEvent.Action.Position + spawner.Offset,
									BaseYOffset = Const.PickableYOffset,
								});
							}
							if (spawner.Capacity == 0 && spawner.VariantCapacity == 0)
							{
								// remove action type
								ref InteractableComponent interactable = ref InteractableLookup.GetRefRW(actionEvent.Target).ValueRW;
								interactable.Flags &= ~ActionType.Collect;
								interactable.Changed = true;
							}
						}
						else if (actionEvent.Type == ActionType.Pick)
						{
							// verify target has not been picked by another event
							if (PickableLookup.IsComponentEnabled(actionEvent.Target))
							{
								continue;
							}

							ref PickComponent pick = ref PickLookup.GetRefRW(actionEvent.Source).ValueRW;
							pick.Picked = actionEvent.Target;
							InteractableComponent interactable = InteractableLookup[actionEvent.Target];
							pick.Flags = interactable.Flags;
							pick.Time = interactable.Time;
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
							position.BaseYOffset = Const.PickableYOffset;
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

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void IncreaseCapacity(Entity entity)
			{
				ref SpawnerComponent spawner = ref SpawnerLookup.GetRefRW(entity).ValueRW;
				if (spawner.Capacity == 0)
				{
					// add action type
					ref InteractableComponent interactable = ref InteractableLookup.GetRefRW(entity).ValueRW;
					if (!interactable.Immutable)
					{
						interactable.Flags |= ActionType.Collect;
						interactable.Changed = true;
					}
				}
				spawner.Capacity++;
			}
		}
	}
}