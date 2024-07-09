using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace U0071
{
	public struct PushEvent
	{
		public float2 Direction;
		public Entity Source; // to push back
		public Entity Target;
	}

	public struct CreditsEvent
	{
		public Entity Source; // can be Null
		public Entity Target;
		public int Value;
	}

	public struct PickDropEvent
	{
		public float2 Position;
		public Entity Source;
		public Entity Target;
		public bool Pick;
	}

	public struct ChangeInteractableEvent
	{
		public Entity Target;
		public ActionFlag FlagsToAdd;
		public ActionFlag FlagsToRemove;
	}

	public struct SpawnerEvent
	{
		public float2 Position;
		public Entity Target;
		public int CapacityChange;
		public bool Spawn;
		// TODO: used item flag (for variant capacity increase)
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct ActionSystem : ISystem
	{
		// event queues
		private NativeQueue<PushEvent> _pushEvents;
		private NativeQueue<CreditsEvent> _creditsEvents;
		private NativeQueue<PickDropEvent> _pickDropEvents;
		private NativeQueue<SpawnerEvent> _spawnerEvents;
		private NativeQueue<ChangeInteractableEvent> _changeInteractableEvents;

		// lookups (alot)
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<CarryComponent> _pickLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<PartitionComponent> _partitionLookup;
		private ComponentLookup<CreditsComponent> _creditsLookup;
		private ComponentLookup<SpawnerComponent> _spawnerLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;
		private ComponentLookup<PushedComponent> _pushedLookup;
		private ComponentLookup<StorageComponent> _storageLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<RoomPartition>();

			_pushEvents = new NativeQueue<PushEvent>(Allocator.Persistent);
			_creditsEvents = new NativeQueue<CreditsEvent>(Allocator.Persistent);
			_pickDropEvents = new NativeQueue<PickDropEvent>(Allocator.Persistent);
			_spawnerEvents = new NativeQueue<SpawnerEvent>(Allocator.Persistent);
			_changeInteractableEvents = new NativeQueue<ChangeInteractableEvent>(Allocator.Persistent);

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>();
			_pickLookup = state.GetComponentLookup<CarryComponent>();
			_pickableLookup = state.GetComponentLookup<PickableComponent>();
			_positionLookup = state.GetComponentLookup<PositionComponent>();
			_partitionLookup = state.GetComponentLookup<PartitionComponent>();
			_creditsLookup = state.GetComponentLookup<CreditsComponent>();
			_spawnerLookup = state.GetComponentLookup<SpawnerComponent>();
			_interactableLookup = state.GetComponentLookup<InteractableComponent>();
			_pushedLookup = state.GetComponentLookup<PushedComponent>();
			_storageLookup = state.GetComponentLookup<StorageComponent>(true);
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_pushEvents.Dispose();
			_creditsEvents.Dispose();
			_pickDropEvents.Dispose();
			_spawnerEvents.Dispose();
			_changeInteractableEvents.Dispose();
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
			_storageLookup.Update(ref state);
			_pushedLookup.Update(ref state);

			// TODO: have generic events (destroyed, modifyCredits etc) written when processing actions and processed afterwards in // (avoid Lookup-fest)
			// TBD: use Ecb instead of Lookup-fest ?

			state.Dependency = new ResolveActionJob
			{
				PushEvents = _pushEvents.AsParallelWriter(),
				CreditsEvents = _creditsEvents.AsParallelWriter(),
				PickDropEvents = _pickDropEvents.AsParallelWriter(),
				SpawnerEvents = _spawnerEvents.AsParallelWriter(),
				StorageLookup = _storageLookup,
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new ResolvePickedActionJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new StopActionJob().ScheduleParallel(state.Dependency);

			state.Dependency = new PushEventJob
			{
				Events = _pushEvents,
				PushedLookup = _pushedLookup,
			}.Schedule(state.Dependency);

			state.Dependency = new CreditsEventJob
			{
				Events = _creditsEvents,
				ChangedInteractableEvents = _changeInteractableEvents.AsParallelWriter(),
				CreditsLookup = _creditsLookup,
			}.Schedule(state.Dependency);

			state.Dependency = new PickDropEventJob
			{
				Events = _pickDropEvents,
				RoomElementLookup = _roomElementLookup,
				InteractableLookup = _interactableLookup,
				PickLookup = _pickLookup,
				PickableLookup = _pickableLookup,
				PartitionLookup = _partitionLookup,
				PositionLookup = _positionLookup,
				Partition = SystemAPI.GetSingleton<RoomPartition>(),
			}.Schedule(state.Dependency);

			state.Dependency = new SpawnerEventJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged),
				Events = _spawnerEvents,
				ChangedInteractableEvents = _changeInteractableEvents.AsParallelWriter(),
				SpawnerLookup = _spawnerLookup,
			}.Schedule(state.Dependency);

			state.Dependency = new ChangeInteractableEventJob
			{
				Events = _changeInteractableEvents,
				InteractableLookup = _interactableLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		[WithAll(typeof(IsActing))]
		public partial struct ResolveActionJob : IJobEntity
		{
			public NativeQueue<PushEvent>.ParallelWriter PushEvents;
			public NativeQueue<CreditsEvent>.ParallelWriter CreditsEvents;
			public NativeQueue<PickDropEvent>.ParallelWriter PickDropEvents;
			public NativeQueue<SpawnerEvent>.ParallelWriter SpawnerEvents;
			[ReadOnly]
			public ComponentLookup<StorageComponent> StorageLookup;
			public float DeltaTime;

			public void Execute(
				Entity entity,
				ref ActionController controller,
				ref HungerComponent hunger,
				in PositionComponent position,
				in CreditsComponent credits)
			{
				// do not filter isDeadTag to be able to drop carried item on death
				// (isActing will filter the job after)

				controller.Timer += DeltaTime;
				if (controller.ShouldResolve(credits.Value))
				{
					// process behavior that can be in parallel here
					// queue the rest

					if (controller.Action.ActionFlag == ActionFlag.Eat)
					{
						hunger.Value += Const.EatingHungerGain;
					}
					else if (controller.Action.ActionFlag == ActionFlag.Push)
					{
						PushEvents.Enqueue(new PushEvent
						{
							Source = entity,
							Target = controller.Action.Target,
							Direction = math.normalizesafe(controller.Action.Position - position.Value),
						});
					}
					else if (controller.Action.ActionFlag == ActionFlag.Search)
					{
						CreditsEvents.Enqueue(new CreditsEvent
						{
							// target/source are switched for credits gain
							Source = controller.Action.Target,
							Target = entity,
							Value = controller.Action.Cost,
						});
					}
					else if (controller.Action.ActionFlag == ActionFlag.Pick || controller.Action.ActionFlag == ActionFlag.Drop)
					{
						PickDropEvents.Enqueue(new PickDropEvent
						{
							Source = entity,
							Target = controller.Action.Target,
							Position = controller.Action.Position,
							Pick = controller.Action.ActionFlag == ActionFlag.Pick,
						});
					}
					else if (controller.Action.ActionFlag == ActionFlag.Store)
					{
						StorageComponent storage = StorageLookup[controller.Action.Target];
						SpawnerEvents.Enqueue(new SpawnerEvent
						{
							Target = storage.Destination,
							CapacityChange = 1,
						});
						if (storage.SecondaryDestination != Entity.Null)
						{
							SpawnerEvents.Enqueue(new SpawnerEvent
							{
								Target = storage.SecondaryDestination,
								CapacityChange = 1,
							});
						}
					}
					else if (controller.Action.ActionFlag == ActionFlag.Collect)
					{
						SpawnerEvents.Enqueue(new SpawnerEvent
						{
							Target = controller.Action.Target,
							Position = controller.Action.Position,
							Spawn = true,
							CapacityChange = -1,
						});
					}

					if (controller.Action.Cost != 0)
					{
						CreditsEvents.Enqueue(new CreditsEvent
						{
							Source = Entity.Null, // money printing/vanishing
							Target = entity,
							Value = controller.Action.Cost,
						});
					}
				}
			}
		}

		[BurstCompile]
		[WithAll(typeof(IsActing))]
		public partial struct ResolvePickedActionJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;

			public void Execute(
				[ChunkIndexInQuery] int chunkIndex,
				ref ActionController controller,
				ref CarryComponent carry,
				in CreditsComponent credits,
				EnabledRefRW<CarryComponent> pickRef)
			{
				if (controller.ShouldResolve(credits.Value) &&
					(controller.Action.ActionFlag == ActionFlag.Destroy || controller.Action.ActionFlag == ActionFlag.Store || controller.Action.ActionFlag == ActionFlag.Eat))
				{
					// destroy used item
					Ecb.DestroyEntity(chunkIndex, carry.Picked);
					carry.Picked = Entity.Null;
					carry.Flags = 0;
					carry.Time = 0f;
					pickRef.ValueRW = false;
				}
			}
		}

		[BurstCompile]
		public partial struct StopActionJob : IJobEntity
		{
			public void Execute(ref ActionController controller, EnabledRefRW<IsActing> isActing)
			{
				if (controller.Timer >= controller.Action.Time)
				{
					controller.Stop();
					isActing.ValueRW = false;
				}
			}
		}

		[BurstCompile]
		public partial struct PushEventJob : IJob
		{
			public NativeQueue<PushEvent> Events;
			public ComponentLookup<PushedComponent> PushedLookup;

			public void Execute()
			{
				NativeArray<PushEvent> events = Events.ToArray(Allocator.Temp);
				using (var enumerator = events.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						PushEvent pushEvent = enumerator.Current;

						ref PushedComponent pushed = ref PushedLookup.GetRefRW(pushEvent.Target).ValueRW;
						pushed.Direction = pushEvent.Direction;
						pushed.Timer = Const.PushedTimer;
						PushedLookup.SetComponentEnabled(pushEvent.Target, true);
					}
				}
				Events.Clear();
				events.Dispose();
			}
		}

		[BurstCompile]
		public partial struct CreditsEventJob : IJob
		{
			public EntityCommandBuffer Ecb;
			public NativeQueue<CreditsEvent> Events;
			public NativeQueue<ChangeInteractableEvent>.ParallelWriter ChangedInteractableEvents;
			public ComponentLookup<CreditsComponent> CreditsLookup;

			public void Execute()
			{
				NativeArray<CreditsEvent> events = Events.ToArray(Allocator.Temp);
				using (var enumerator = events.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						CreditsEvent creditsEvent = enumerator.Current;
						int value = creditsEvent.Value;

						if (creditsEvent.Source != Entity.Null)
						{
							// clamp value to source credits and decrease source credits

							ref CreditsComponent credits = ref CreditsLookup.GetRefRW(creditsEvent.Target).ValueRW;
							value = math.min(math.max(0, credits.Value), value);
							credits.Value -= value;

							if (credits.Value <= 0f)
							{
								ChangedInteractableEvents.Enqueue(new ChangeInteractableEvent
								{
									Target = creditsEvent.Source,
									FlagsToRemove = ActionFlag.Search, // TBD: only for AI (empty search for player)
								});
							}
						}
						if (value != 0)
						{
							CreditsLookup.GetRefRW(creditsEvent.Target).ValueRW.Value += value;
						}
					}
				}
				Events.Clear();
				events.Dispose();
			}
		}

		[BurstCompile]
		public partial struct PickDropEventJob : IJob
		{
			public EntityCommandBuffer Ecb;
			public NativeQueue<PickDropEvent> Events;
			public BufferLookup<RoomElementBufferElement> RoomElementLookup;
			public ComponentLookup<CarryComponent> PickLookup;
			public ComponentLookup<PickableComponent> PickableLookup;
			public ComponentLookup<PartitionComponent> PartitionLookup;
			public ComponentLookup<PositionComponent> PositionLookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup;
			[ReadOnly]
			public RoomPartition Partition;

			public void Execute()
			{
				NativeArray<PickDropEvent> events = Events.ToArray(Allocator.Temp);
				using (var enumerator = events.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						PickDropEvent pickDropEvent = enumerator.Current;

						if (pickDropEvent.Pick)
						{
							// verify target has not been picked before
							if (PickableLookup.IsComponentEnabled(pickDropEvent.Target))
							{
								continue;
							}

							ref CarryComponent carry = ref PickLookup.GetRefRW(pickDropEvent.Source).ValueRW;
							carry.Picked = pickDropEvent.Target;
							InteractableComponent interactable = InteractableLookup[pickDropEvent.Target];
							carry.Flags = interactable.ItemFlags;
							carry.Time = interactable.Time;
							PickLookup.SetComponentEnabled(pickDropEvent.Source, true);

							PickableLookup.GetRefRW(pickDropEvent.Target).ValueRW.Carrier = pickDropEvent.Source;
							PickableLookup.SetComponentEnabled(pickDropEvent.Target, true);

							Entity room = Partition.GetRoomData(pickDropEvent.Position).Entity;
							if (room != Entity.Null)
							{
								DynamicBuffer<RoomElementBufferElement> roomElements = RoomElementLookup[room];
								RoomElementBufferElement.RemoveElement(ref roomElements, new RoomElementBufferElement(pickDropEvent.Target));
								PartitionLookup.GetRefRW(pickDropEvent.Target).ValueRW.CurrentRoom = Entity.Null;
							}
						}
						else // drop
						{
							ref CarryComponent carry = ref PickLookup.GetRefRW(pickDropEvent.Source).ValueRW;
							carry.Picked = Entity.Null;
							carry.Flags = 0;
							PickLookup.SetComponentEnabled(pickDropEvent.Source, false);
							PickableLookup.GetRefRW(pickDropEvent.Target).ValueRW.Carrier = Entity.Null;
							PickableLookup.SetComponentEnabled(pickDropEvent.Target, false);

							ref PositionComponent position = ref PositionLookup.GetRefRW(pickDropEvent.Target).ValueRW;
							position.Value = pickDropEvent.Position;
							position.BaseYOffset = Const.PickableYOffset;
						}
					}
				}
				Events.Clear();
				events.Dispose();
			}
		}

		[BurstCompile]
		public partial struct SpawnerEventJob : IJob
		{
			public EntityCommandBuffer Ecb;
			public NativeQueue<SpawnerEvent> Events;
			public NativeQueue<ChangeInteractableEvent>.ParallelWriter ChangedInteractableEvents;
			public ComponentLookup<SpawnerComponent> SpawnerLookup;

			public void Execute()
			{
				NativeArray<SpawnerEvent> events = Events.ToArray(Allocator.Temp);
				using (var enumerator = events.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						// spawning and capacity changed are processed
						// together because spawning depends on capacity (able, variant)

						SpawnerEvent spawnerEvent = enumerator.Current;

						ref SpawnerComponent spawner = ref SpawnerLookup.GetRefRW(spawnerEvent.Target).ValueRW;

						// TODO: variant capacity/spawn

						if (spawnerEvent.Spawn && spawner.Capacity > 0)
						{
							Ecb.SetComponent(Ecb.Instantiate(spawner.Prefab), new PositionComponent
							{
								Value = spawnerEvent.Position + spawner.Offset,
								BaseYOffset = Const.PickableYOffset,
							});
						}

						int newCapacity = spawner.Capacity + spawnerEvent.CapacityChange;

						if (!spawner.Immutable && spawner.Capacity <= 0 && newCapacity > 0)
						{
							ChangedInteractableEvents.Enqueue(new ChangeInteractableEvent
							{
								Target = spawnerEvent.Target,
								FlagsToAdd = ActionFlag.Collect,
							});
						}
						else if (!spawner.Immutable && spawner.Capacity > 0 && newCapacity <= 0)
						{
							ChangedInteractableEvents.Enqueue(new ChangeInteractableEvent
							{
								Target = spawnerEvent.Target,
								FlagsToRemove = ActionFlag.Collect,
							});
						}

						spawner.Capacity = newCapacity;
					}
				}
				Events.Clear();
				events.Dispose();
			}
		}

		[BurstCompile]
		public partial struct ChangeInteractableEventJob : IJob
		{
			public NativeQueue<ChangeInteractableEvent> Events;
			public ComponentLookup<InteractableComponent> InteractableLookup;

			public void Execute()
			{
				NativeArray<ChangeInteractableEvent> events = Events.ToArray(Allocator.Temp);
				using (var enumerator = events.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						ChangeInteractableEvent changeInteractableEvent = enumerator.Current;

						ref InteractableComponent interactable = ref InteractableLookup.GetRefRW(changeInteractableEvent.Target).ValueRW;

						interactable.ActionFlags |= changeInteractableEvent.FlagsToAdd;
						interactable.ActionFlags &= ~changeInteractableEvent.FlagsToRemove;
						interactable.Changed = true;
					}
				}
				events.Dispose();
				Events.Clear();
			}
		}
	}
}