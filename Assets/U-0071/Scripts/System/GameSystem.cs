using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = Unity.Mathematics.Random;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(GameInitSystem))]
	public partial struct GameSimulationSystem : ISystem
	{
		private uint _seed;
		private int _iteration;
		private float _resetTimer;
		private bool _resetStarted;
		private bool _iterationInitialized;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
			state.RequireForUpdate<PlayerController>();

			_seed = new Random(math.clamp((uint)(Time.realtimeSinceStartup * 100000f), 1, uint.MaxValue)).NextUInt(1, uint.MaxValue);
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			if (!_iterationInitialized)
			{
				_iterationInitialized = true;
				ref Config config = ref SystemAPI.GetSingletonRW<Config>().ValueRW;
				config.Seed = _seed;
				config.Iteration = _iteration;
				Debug.Log("Iteration " + config.Iteration + " initialized with seed " +  config.Seed);
			}

			if (_resetStarted)
			{
				_resetTimer += SystemAPI.Time.DeltaTime;
				if (_resetTimer > Const.SimulationResetTime)
				{
					_iterationInitialized = false;
					_resetStarted = false;
					_resetTimer = 0;
					_iteration++;
					SceneManager.LoadScene("Testing", LoadSceneMode.Single);
				}
			}
			else
			{
				Entity player = SystemAPI.GetSingletonEntity<PlayerController>();
				if (state.EntityManager.IsComponentEnabled<DeathComponent>(player))
				{
					_resetStarted = true;
				}
			}
		}
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(RoomSystem))]
	public partial struct GameInitSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
			state.RequireForUpdate<GameInitFlag>();
		}
		
		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			if (SystemAPI.HasComponent<Partition>(state.SystemHandle))
			{
				SystemAPI.GetComponent<Partition>(state.SystemHandle).Dispose();
				SystemAPI.GetComponent<FlowfieldCollection>(state.SystemHandle).Dispose();
			}
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			Config config = SystemAPI.GetSingleton<Config>();
			Partition partition = new Partition(config.WorldDimensions);
			FlowfieldCollection flowfieldCollection = new FlowfieldCollection(config.WorldDimensions);
			int size = config.WorldDimensions.x * config.WorldDimensions.y;

			// wander path (looping) is common to all authorization (will be in lower)
			// it will then be overlayed on each flowfield "ToWander" path
			// (get out of the rooms => wander in loop)
			NativeArray<bool> wanderCells = new NativeArray<bool>(size, Allocator.TempJob);
			NativeArray<float2> wanderPath = new NativeArray<float2>(size, Allocator.TempJob);
			NativeQueue<WandererComponent> wanderers = new NativeQueue<WandererComponent>(Allocator.TempJob);

			// jobs are run with NativeDisableParallelForRestriction on the partition
			// because rooms should not be on top of each other
			// (isolated cell indexes)
			// (same for devices)
			// (otherwise map is wrong and will cause a big mess)
			// (TODO: in a proper project, editor tool detecting room or device overlaps)

			// by design, flowfields area organized incrementally (one < two < three)
			// but they could be used as exclusion (separated workforces)
			// (was the original jam goal)
			// (would need to tweak area authorization comparison)

			// partition/rooms init
			new RoomInitJob
			{
				Partition = partition,
				WanderCells = wanderCells,
			}.ScheduleParallel(state.Dependency).Complete();
			new CorridorOverrideJob
			{
				Partition = partition,
				WanderCells = wanderCells,
			}.ScheduleParallel(state.Dependency).Complete();

			// wander path
			new GetWanderersJob
			{
				Wanderers = wanderers.AsParallelWriter(),
			}.Schedule(state.Dependency).Complete();
			new WanderPathJob
			{
				WanderCells = wanderCells,
				WanderPath = wanderPath,
				Wanderers = wanderers,
				Dimensions = partition.Dimensions,
			}.Schedule(state.Dependency).Complete();

			// flowfields init
			// run sequentially but each call run its jobs in parallel
			ProcessFlowfield(ref state, ref flowfieldCollection.LevelOne, AreaAuthorization.LevelOne, in wanderPath, in partition);
			ProcessFlowfield(ref state, ref flowfieldCollection.LevelTwo, AreaAuthorization.LevelTwo, in wanderPath, in partition);
			ProcessFlowfield(ref state, ref flowfieldCollection.LevelThree, AreaAuthorization.LevelThree, in wanderPath, in partition);
			ProcessFlowfield(ref state, ref flowfieldCollection.Admin, AreaAuthorization.Admin, in wanderPath, in partition);

			wanderCells.Dispose();
			wanderPath.Dispose();
			wanderers.Dispose();

			state.EntityManager.AddComponentData(state.SystemHandle, partition);
			state.EntityManager.AddComponentData(state.SystemHandle, flowfieldCollection);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ProcessFlowfield(
			ref SystemState state, 
			ref Flowfield flowfield, 
			AreaAuthorization areaFlag, 
			in NativeArray<float2> wanderPath, 
			in Partition partition)
		{
			int size = flowfield.Dimensions.x * flowfield.Dimensions.y;

			// separated for // processing, will be merged after
			// do not use ActionFlag.Collect (not set if distributor starting capacity != 0)
			// (hack: WorkingStationgFlag is used instead)
			FlowfieldBuilder toFood = new FlowfieldBuilder(areaFlag, 0, ItemFlag.Food, in partition);
			FlowfieldBuilder toWork = new FlowfieldBuilder(areaFlag, 0, 0, in partition, true);
			FlowfieldBuilder toDestroy = new FlowfieldBuilder(areaFlag, ActionFlag.Destroy, ItemFlag.Trash, in partition);
			FlowfieldBuilder toProcess = new FlowfieldBuilder(areaFlag, ActionFlag.Store, ItemFlag.RawFood, in partition);
			FlowfieldBuilder toWander = new FlowfieldBuilder(areaFlag, 0, 0, in partition);
			FlowfieldBuilder toRelax = new FlowfieldBuilder(areaFlag, 0, 0, in partition); // TODO

			state.Dependency = new DeviceFlowfieldInitJob
			{
				Partition = partition,
				ToFoodBuilder = toFood,
				ToWorkBuilder = toWork,
				ToDestroyBuilder = toDestroy,
				ToProcessBuilder = toProcess,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new WandererFlowfieldInitJob
			{
				ToWanderBuilder = toWander,
				ToRelaxBuilder = toRelax, // TEMP, TODO: relax devices/spots (for now will run away from working stations)
			}.Schedule(state.Dependency);

			state.Dependency = new FlowfieldSpreadJob { Builder = toFood }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = toWork }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = toDestroy }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = toProcess }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = toWander }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = toRelax }.Schedule(state.Dependency);

			state.Dependency = new FlowfieldDirectionJob { Builder = toFood }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = toWork }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = toDestroy }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = toProcess }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = toWander }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = toRelax }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);

			state.Dependency.Complete();

			// merge flowfields + wander path
			for (int i = 0; i < size; i++)
			{
				float2 wanderDirection = wanderPath[i];
				flowfield.Cells[i] = new FlowfieldCell
				{
					ToFood = toFood.Flowfield[i],
					ToWork = toWork.Flowfield[i],
					ToDestroy = toDestroy.Flowfield[i],
					ToProcess = toProcess.Flowfield[i],
					ToRelax = toRelax.Flowfield[i],

					// overlay goToWander on wander flowfield
					// (get out of the rooms => wander in loop)
					ToWander = wanderDirection.Equals(float2.zero) ? toWander.Flowfield[i] : wanderDirection,
				};
			}

			toFood.Dispose();
			toWork.Dispose();
			toDestroy.Dispose();
			toProcess.Dispose();
			toWander.Dispose();
			toRelax.Dispose();
		}

		[BurstCompile]
		public partial struct RoomInitJob : IJobEntity
		{
			[NativeDisableParallelForRestriction] public Partition Partition;
			[NativeDisableParallelForRestriction] public NativeArray<bool> WanderCells;

			public void Execute(Entity entity, in PositionComponent position, in RoomComponent room)
			{
				for (int y = 0; y < room.Dimensions.y; y++)
				{
					for (int x = 0; x < room.Dimensions.x; x++)
					{
						int index = Partition.GetIndex(new float2(position.x + x - room.Dimensions.x / 2f, position.y + y - room.Dimensions.y / 2f));

						Partition.SetCellData(
							true,
							new RoomData
							{
								Entity = entity,
								Position = position.Value,
								Room = room,
							},
							index);

						if (room.IsWanderPath && index >= 0 && index < WanderCells.Length)
						{
							WanderCells[index] = true;
						}
					}
				}
			}
		}

		[BurstCompile]
		public partial struct CorridorOverrideJob : IJobEntity
		{
			[NativeDisableParallelForRestriction] public Partition Partition;
			[NativeDisableParallelForRestriction] public NativeArray<bool> WanderCells;

			public void Execute(in CorridorOverrideComponent corridorOverride)
			{
				for (int y = 0; y < corridorOverride.Dimensions.y; y++)
				{
					for (int x = 0; x < corridorOverride.Dimensions.x; x++)
					{
						int index = Partition.GetIndex(new float2(corridorOverride.Position.x + x - corridorOverride.Dimensions.x / 2f, corridorOverride.Position.y + y - corridorOverride.Dimensions.y / 2f));
						if (Partition.IsPathable(index) && index >= 0 && index < WanderCells.Length)
						{
							WanderCells[index] = true;
						}
					}
				}
			}
		}

		[BurstCompile]
		public partial struct GetWanderersJob : IJobEntity
		{
			public NativeQueue<WandererComponent>.ParallelWriter Wanderers;

			public void Execute(in WandererComponent wanderer)
			{
				Wanderers.Enqueue(wanderer);
			}
		}

		[BurstCompile]
		public partial struct WanderPathJob : IJob
		{
			[NativeDisableParallelForRestriction] public NativeArray<bool> WanderCells;
			[NativeDisableParallelForRestriction] public NativeArray<float2> WanderPath;
			public NativeQueue<WandererComponent> Wanderers;
			public int2 Dimensions;

			public void Execute()
			{
				while (Wanderers.Count > 0)
				{
					WandererComponent wanderer = Wanderers.Dequeue();
					float2 direction = float2.zero;
					if (!TryWander(in wanderer, new float2(wanderer.Direction.y, -wanderer.Direction.x), ref direction) && // right
						!TryWander(in wanderer, wanderer.Direction, ref direction)) // front
					{
						TryWander(in wanderer, new float2(-wanderer.Direction.y, wanderer.Direction.x), ref direction); // left
					}
					WanderPath[GetIndex(wanderer.Position)] = direction;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private bool TryWander(in WandererComponent wanderer, float2 newDirection, ref float2 bestDirection)
			{
				float2 newPosition = wanderer.Position + newDirection;
				int newIndex = GetIndex(newPosition);
				if (newIndex >= 0 && newIndex < WanderCells.Length && WanderCells[newIndex])
				{
					if (WanderPath[newIndex].Equals(float2.zero))
					{
						bestDirection = newDirection;
						Wanderers.Enqueue(new WandererComponent
						{
							Position = newPosition,
							Direction = newDirection,
						});
						return true;
					}
					else if (bestDirection.Equals(float2.zero))
					{
						bestDirection = newDirection;
					}
				}
				return false;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private int GetIndex(float2 position)
			{
				return (int)(position.x + Dimensions.x / 2) + (int)(position.y + Dimensions.y / 2) * Dimensions.x;
			}
		}
		
		[BurstCompile]
		[WithAll(typeof(DeviceTag))]
		public partial struct DeviceFlowfieldInitJob : IJobEntity
		{
			[ReadOnly] public Partition Partition;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder ToFoodBuilder;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder ToWorkBuilder;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder ToDestroyBuilder;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder ToProcessBuilder;

			public void Execute(in InteractableComponent interactable, in LocalTransform transform)
			{
				// we assume normalized scale
				float2 position = new float2(transform.Position.x, transform.Position.z);
				int size = (int)transform.Scale;

				ToFoodBuilder.ProcessDevice(in interactable, in Partition, position, size);
				ToDestroyBuilder.ProcessDevice(in interactable, in Partition, position, size);
				ToProcessBuilder.ProcessDevice(in interactable, in Partition, position, size);
				ToWorkBuilder.ProcessDevice(in interactable, in Partition, position, size);
			}
		}

		[BurstCompile]
		public partial struct WandererFlowfieldInitJob : IJobEntity
		{
			[NativeDisableParallelForRestriction] public FlowfieldBuilder ToWanderBuilder;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder ToRelaxBuilder; // TEMP

			public void Execute(in WandererComponent wanderer)
			{
				ToWanderBuilder.InitStartingCell(ToWanderBuilder.GetIndex(wanderer.Position), true);
				ToRelaxBuilder.InitStartingCell(ToRelaxBuilder.GetIndex(wanderer.Position), true);
			}
		}

		[BurstCompile]
		public partial struct FlowfieldSpreadJob : IJob
		{
			public FlowfieldBuilder Builder;

			public void Execute()
			{
				Builder.Spread();
			}
		}

		[BurstCompile]
		public partial struct FlowfieldDirectionJob : IJobFor
		{
			[NativeDisableParallelForRestriction]
			public FlowfieldBuilder Builder;

			public void Execute(int index)
			{
				Builder.ProcessDirection(index);
			}
		}
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(GameInitSystem))]
	[UpdateBefore(typeof(AIControllerSystem))]
	public partial struct UnitIdentityInitSystem : ISystem
	{
		private BlobAssetReference<UnitIdentityCollection> _unitNamesReference;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
			state.RequireForUpdate<GameInitFlag>();
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			if (_unitNamesReference.IsCreated)
			{
				_unitNamesReference.Dispose();
			}
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// generate AI identities (name, pilosity, colors)
			// need to be deterministic per seed

			ref Config config = ref SystemAPI.GetSingletonRW<Config>().ValueRW;
			ManagedData managedData = ManagedData.Instance;

			var builder = new BlobBuilder(Allocator.Temp);
			ref UnitIdentityCollection blobAsset = ref builder.ConstructRoot<UnitIdentityCollection>();

			BlobBuilderArray<float4> skinColorArrayBuilder = builder.Allocate(ref blobAsset.SkinColors, managedData.SkinColors.Length);
			for (int i = 0; i < managedData.SkinColors.Length; i++)
			{
				skinColorArrayBuilder[i] = managedData.SkinColors[i].linear.ToFloat4();
			}
			BlobBuilderArray<float4> hairColorArrayBuilder = builder.Allocate(ref blobAsset.HairColors, managedData.HairColors.Length);
			for (int i = 0; i < managedData.HairColors.Length; i++)
			{
				hairColorArrayBuilder[i] = managedData.HairColors[i].linear.ToFloat4();
			}

			Random random = new Random(config.Seed);
			NativeArray<UnitIdentity> identities = new NativeArray<UnitIdentity>(9999, Allocator.Temp);
			for (int i = 0; i < identities.Length; i++)
			{
				identities[i] = new UnitIdentity
				{
					Name = i != 71 ? "U-" + i.ToString("0000") : new FixedString32Bytes("U-9999"), // avoid U-0071
					SkinColorIndex = random.NextInt(managedData.SkinColors.Length),
					HairColorIndex = random.NextInt(managedData.HairColors.Length),
					HasShortHair = random.NextFloat() <= config.ChanceOfShortHair,
					HasLongHair = random.NextFloat() <= config.ChanceOfLongHair,
					HasBeard = random.NextFloat() <= config.ChanceOfBeard,
				};
			}

			// shuffle
			for (int i = 0; i < identities.Length; i++)
			{
				UnitIdentity temp = identities[i];
				int index = random.NextInt(identities.Length);
				identities[i] = identities[index];
				identities[index] = temp;
			}

			BlobBuilderArray<UnitIdentity> identityArrayBuilder = builder.Allocate(ref blobAsset.Identities, identities.Length);
			for (int i = 0; i < identities.Length; i++)
			{
				identityArrayBuilder[i] = identities[i];
			}

			_unitNamesReference = builder.CreateBlobAssetReference<UnitIdentityCollection>(Allocator.Persistent);
			config.UnitIdentityData = _unitNamesReference;
			builder.Dispose();
			identities.Dispose();

			// last system to initialize
			state.EntityManager.RemoveComponent<GameInitFlag>(SystemAPI.GetSingletonEntity<Config>());
		}
	}
}