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

	public struct TeleportEvent
	{
		public Entity Source;
		public Entity Target;
		public Entity Picked;
	}

	public struct OpenEvent
	{
		public Entity Target;
	}

	public struct ChangeInteractableEvent
	{
		public Entity Target;
		public Entity CurrentUser;
		public ActionFlag FlagsToAdd;
		public ActionFlag FlagsToRemove;
	}

	public struct SpawnerEvent
	{
		public float2 Position;
		public Entity Target;
		public int CapacityChange;
		public int VariantCapacityChange;
		public bool Spawn;
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
		private NativeQueue<TeleportEvent> _teleportEvents;
		private NativeQueue<OpenEvent> _openEvents;
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
		private ComponentLookup<TeleporterComponent> _teleporterLookup;
		private ComponentLookup<StorageComponent> _storageLookup;
		private ComponentLookup<DoorComponent> _doorLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Partition>();

			_pushEvents = new NativeQueue<PushEvent>(Allocator.Persistent);
			_creditsEvents = new NativeQueue<CreditsEvent>(Allocator.Persistent);
			_pickDropEvents = new NativeQueue<PickDropEvent>(Allocator.Persistent);
			_spawnerEvents = new NativeQueue<SpawnerEvent>(Allocator.Persistent);
			_teleportEvents = new NativeQueue<TeleportEvent>(Allocator.Persistent);
			_openEvents = new NativeQueue<OpenEvent>(Allocator.Persistent);
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
			_teleporterLookup = state.GetComponentLookup<TeleporterComponent>();
			_doorLookup = state.GetComponentLookup<DoorComponent>();
			_storageLookup = state.GetComponentLookup<StorageComponent>(true);
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_pushEvents.Dispose();
			_creditsEvents.Dispose();
			_pickDropEvents.Dispose();
			_spawnerEvents.Dispose();
			_teleportEvents.Dispose();
			_openEvents.Dispose();
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
			_teleporterLookup.Update(ref state);
			_pushedLookup.Update(ref state);
			_doorLookup.Update(ref state);

			// TODO: have generic events (destroyed, modifyCredits etc) written when processing actions and processed afterwards in // (avoid Lookup-fest)
			// TBD: use Ecb instead of Lookup-fest ?

			state.Dependency = new ResolveActionJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				PushEvents = _pushEvents.AsParallelWriter(),
				CreditsEvents = _creditsEvents.AsParallelWriter(),
				PickDropEvents = _pickDropEvents.AsParallelWriter(),
				SpawnerEvents = _spawnerEvents.AsParallelWriter(),
				TeleportEvents = _teleportEvents.AsParallelWriter(),
				OpenEvents = _openEvents.AsParallelWriter(),
				ChangeInteractableEvents = _changeInteractableEvents.AsParallelWriter(),
				StorageLookup = _storageLookup,
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new ResolvePickedActionJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new StopActionJob
			{
				ChangeInteractableEvents = _changeInteractableEvents.AsParallelWriter(),
			}.ScheduleParallel(state.Dependency);

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
				Partition = SystemAPI.GetSingleton<Partition>(),
			}.Schedule(state.Dependency);

			state.Dependency = new SpawnerEventJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged),
				Events = _spawnerEvents,
				ChangedInteractableEvents = _changeInteractableEvents.AsParallelWriter(),
				SpawnerLookup = _spawnerLookup,
			}.Schedule(state.Dependency);

			state.Dependency = new TeleportEventJob
			{
				Events = _teleportEvents,
				TeleporterLookup = _teleporterLookup,
				PickLookup = _pickLookup,
				PickableLookup = _pickableLookup,
				PositionLookup = _positionLookup,
			}.Schedule(state.Dependency);

			state.Dependency = new OpenEventJob
			{
				Events = _openEvents,
				DoorLookup = _doorLookup,
				ChangedInteractableEvents = _changeInteractableEvents.AsParallelWriter(),
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
			public EntityCommandBuffer.ParallelWriter Ecb;
			public NativeQueue<PushEvent>.ParallelWriter PushEvents;
			public NativeQueue<CreditsEvent>.ParallelWriter CreditsEvents;
			public NativeQueue<PickDropEvent>.ParallelWriter PickDropEvents;
			public NativeQueue<SpawnerEvent>.ParallelWriter SpawnerEvents;
			public NativeQueue<TeleportEvent>.ParallelWriter TeleportEvents;
			public NativeQueue<OpenEvent>.ParallelWriter OpenEvents;
			public NativeQueue<ChangeInteractableEvent>.ParallelWriter ChangeInteractableEvents;
			[ReadOnly]
			public ComponentLookup<StorageComponent> StorageLookup;
			public float DeltaTime;

			public void Execute(
				[ChunkIndexInQuery] int chunkIndex,
				Entity entity,
				ref ActionController controller,
				ref HungerComponent hunger,
				in PositionComponent position,
				in CreditsComponent credits)
			{
				// do not filter isDeadTag to be able to drop carried item on death
				// (isActing will filter the job after)

				if (!controller.Action.MultiusableFlag && controller.Timer == 0f)
				{
					// start using single-used interactable
					ChangeInteractableEvents.Enqueue(new ChangeInteractableEvent
					{
						Target = controller.Action.Target,
						CurrentUser = entity,
					});
				}

				controller.Timer += DeltaTime;
				if (controller.ShouldResolve(credits.Value))
				{
					// process behavior that can be in parallel here
					// queue the rest

					if (controller.Action.ActionFlag == ActionFlag.Eat)
					{
						if (Utilities.HasItemFlag(controller.Action.UsedItemFlags, ItemFlag.Contaminated))
						{
							hunger.Value -= Const.ContaminatedEatingHungerLoss;
							Ecb.SetComponentEnabled<SickComponent>(chunkIndex, entity, true);
						}
						else
						{
							hunger.Value = math.min(Const.MaxHunger, hunger.Value + Const.EatingHungerGain);
						}
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
							CapacityChange = controller.Action.UseVariantSpawn ? 0 : 1,
							VariantCapacityChange = controller.Action.UseVariantSpawn ? 1 : 0,
						});
						if (storage.SecondaryDestination != Entity.Null && !controller.Action.UseVariantSpawn)
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
						});
					}
					else if (controller.Action.ActionFlag == ActionFlag.Teleport)
					{
						TeleportEvents.Enqueue(new TeleportEvent
						{
							Source = entity,
							Target = controller.Action.Target,
						});
					}
					else if (controller.Action.ActionFlag == ActionFlag.Open)
					{
						OpenEvents.Enqueue(new OpenEvent
						{
							Target = controller.Action.Target,
						});
					}

					if (controller.Action.Cost != 0)
					{
						CreditsEvents.Enqueue(new CreditsEvent
						{
							Source = Entity.Null, // money printing/vanishing
							Target = entity,
							Value = -controller.Action.Cost,
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
			public NativeQueue<ChangeInteractableEvent>.ParallelWriter ChangeInteractableEvents;
			
			public void Execute(ref ActionController controller, EnabledRefRW<IsActing> isActing)
			{
				if (controller.ShouldStop)
				{
					if (!controller.Action.MultiusableFlag)
					{
						// interactable is not single-used anymore
						ChangeInteractableEvents.Enqueue(new ChangeInteractableEvent
						{
							Target = controller.Action.Target,
							CurrentUser = Entity.Null,
						});
					}
					controller.Action.Target = Entity.Null;
					controller.Action.ActionFlag = 0;
					controller.IsResolving = false;
					controller.ShouldStopFlag = false;
					controller.ShouldDropFlag = false;
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
				while (Events.Count > 0)
				{
					PushEvent pushEvent = Events.Dequeue();

					ref PushedComponent pushed = ref PushedLookup.GetRefRW(pushEvent.Target).ValueRW;
					pushed.Direction = pushEvent.Direction;
					pushed.Timer = Const.PushedTimer;
					PushedLookup.SetComponentEnabled(pushEvent.Target, true);
				}
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
				while (Events.Count > 0)
				{
					CreditsEvent creditsEvent = Events.Dequeue();
					int value = creditsEvent.Value;

					if (creditsEvent.Source != Entity.Null)
					{
						// clamp value to source credits and decrease source credits

						ref CreditsComponent credits = ref CreditsLookup.GetRefRW(creditsEvent.Source).ValueRW;
						value = math.min(math.max(0, credits.Value), Const.LootCreditsCount);
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
			public Partition Partition;

			public void Execute()
			{
				while (Events.Count > 0)
				{
					PickDropEvent pickDropEvent = Events.Dequeue();

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
				while (Events.Count > 0)
				{
					// spawning and capacity changed are processed
					// together because spawning depends on capacity (able, variant)

					SpawnerEvent spawnerEvent = Events.Dequeue();

					ref SpawnerComponent spawner = ref SpawnerLookup.GetRefRW(spawnerEvent.Target).ValueRW;

					if (spawnerEvent.Spawn && spawner.VariantCapacity > 0)
					{
						Ecb.SetComponent(Ecb.Instantiate(spawner.VariantPrefab), new PositionComponent
						{
							Value = spawnerEvent.Position + spawner.Offset,
							BaseYOffset = Const.PickableYOffset,
						});
						spawnerEvent.VariantCapacityChange--;
					}
					else if (spawnerEvent.Spawn && spawner.Capacity > 0)
					{
						Ecb.SetComponent(Ecb.Instantiate(spawner.Prefab), new PositionComponent
						{
							Value = spawnerEvent.Position + spawner.Offset,
							BaseYOffset = Const.PickableYOffset,
						});
						spawnerEvent.CapacityChange--;
					}

					int newCapacity = spawner.Capacity + spawnerEvent.CapacityChange;
					int newVariantCapacity = spawner.VariantCapacity + spawnerEvent.VariantCapacityChange;

					if (!spawner.Immutable && (spawner.Capacity <= 0 && newCapacity > 0 || spawner.VariantCapacity <= 0 && newVariantCapacity > 0))
					{
						ChangedInteractableEvents.Enqueue(new ChangeInteractableEvent
						{
							Target = spawnerEvent.Target,
							FlagsToAdd = ActionFlag.Collect,
						});
					}
					else if (!spawner.Immutable && spawner.Capacity + spawner.VariantCapacity > 0 && newCapacity + newVariantCapacity <= 0)
					{
						ChangedInteractableEvents.Enqueue(new ChangeInteractableEvent
						{
							Target = spawnerEvent.Target,
							FlagsToRemove = ActionFlag.Collect,
						});
					}

					spawner.Capacity = newCapacity;
					spawner.VariantCapacity = newVariantCapacity;
				}
			}
		}

		[BurstCompile]
		public partial struct TeleportEventJob : IJob
		{
			public NativeQueue<TeleportEvent> Events;
			public ComponentLookup<CarryComponent> PickLookup;
			public ComponentLookup<PickableComponent> PickableLookup;
			public ComponentLookup<PositionComponent> PositionLookup;
			[ReadOnly]
			public ComponentLookup<TeleporterComponent> TeleporterLookup;

			public void Execute()
			{
				while (Events.Count > 0)
				{
					TeleportEvent teleportEvent = Events.Dequeue();

					ref CarryComponent carry = ref PickLookup.GetRefRW(teleportEvent.Source).ValueRW;
					if (carry.Picked != Entity.Null)
					{
						PickableLookup.GetRefRW(carry.Picked).ValueRW.Carrier = Entity.Null;
						PickableLookup.SetComponentEnabled(carry.Picked, false);
					}

					ref PositionComponent position = ref PositionLookup.GetRefRW(carry.Picked).ValueRW;
					position.Value = TeleporterLookup[teleportEvent.Target].Destination;
					position.BaseYOffset = Const.PickableYOffset;

					carry.Picked = Entity.Null;
					carry.Flags = 0;
					PickLookup.SetComponentEnabled(teleportEvent.Source, false);
				}
			}
		}

		[BurstCompile]
		public partial struct OpenEventJob : IJob
		{
			public NativeQueue<OpenEvent> Events;
			public NativeQueue<ChangeInteractableEvent>.ParallelWriter ChangedInteractableEvents;
			public ComponentLookup<DoorComponent> DoorLookup;

			public void Execute()
			{
				while (Events.Count > 0)
				{
					OpenEvent openEvent = Events.Dequeue();

					DoorLookup.SetComponentEnabled(openEvent.Target, true);
					ChangedInteractableEvents.Enqueue(new ChangeInteractableEvent
					{
						Target = openEvent.Target,
						FlagsToRemove = ActionFlag.Open,
					});
				}
			}
		}

		[BurstCompile]
		public partial struct ChangeInteractableEventJob : IJob
		{
			public NativeQueue<ChangeInteractableEvent> Events;
			public ComponentLookup<InteractableComponent> InteractableLookup;

			public void Execute()
			{
				while (Events.Count > 0)
				{
					ChangeInteractableEvent changeInteractableEvent = Events.Dequeue();

					ref InteractableComponent interactable = ref InteractableLookup.GetRefRW(changeInteractableEvent.Target).ValueRW;
					if (!interactable.CanBeMultiused && interactable.CurrentUser != changeInteractableEvent.CurrentUser)
					{
						interactable.CurrentUser = changeInteractableEvent.CurrentUser;
						interactable.Changed = true;
					}
					if (changeInteractableEvent.FlagsToAdd != 0)
					{
						interactable.ActionFlags |= changeInteractableEvent.FlagsToAdd;
						interactable.Changed = true;
					}
					if (changeInteractableEvent.FlagsToRemove != 0)
					{
						interactable.ActionFlags &= ~changeInteractableEvent.FlagsToRemove;
						interactable.Changed = true;
					}
				}
			}
		}
	}
}