using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(RoomSystem))]
	public partial struct GameInitSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Config>();
		}
		
		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			if (SystemAPI.HasComponent<Partition>(state.SystemHandle))
			{
				SystemAPI.GetComponent<Partition>(state.SystemHandle).Dispose();
			}
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			Config config = SystemAPI.GetSingleton<Config>();
			Partition partition = new Partition(config.WorldDimensions);
			Flowfield flowfield = new Flowfield(config.WorldDimensions);

			// jobs are run with NativeDisableParallelForRestriction on the partition
			// because rooms and doors/links should not be on top of one another
			// (isolated cell indexes)
			// (otherwise map is wrong)

			// partition/rooms init

			new RoomInitJob
			{
				Partition = partition,
			}.ScheduleParallel(state.Dependency).Complete();

			new RoomLinkJob
			{
				Partition = partition,
			}.ScheduleParallel(state.Dependency).Complete();

			// flowfields init

			int size = config.WorldDimensions.x * config.WorldDimensions.y;

			FlowfieldBuilder foodLevelZeroBuilder = new FlowfieldBuilder(flowfield.FoodLevelZero, ActionFlag.Collect, ItemFlag.Food);
			FlowfieldBuilder destroyLevelZeroBuilder = new FlowfieldBuilder(flowfield.DestroyLevelZero, ActionFlag.Destroy, ItemFlag.Trash);

			new DeviceFlowfieldInitJob
			{
				Partition = partition,
				FoodLevelZeroBuilder = foodLevelZeroBuilder,
				DestroyLevelZeroBuilder = destroyLevelZeroBuilder,
			}.ScheduleParallel(state.Dependency).Complete();

			// TODO: spread

			// TODO: direction job (in //)

			// TODO: devices as cost field

			// debug
			for (int i = 0; i < size; i++)
			{
				flowfield.FoodLevelZero[i] = new float2(foodLevelZeroBuilder.Values[i], 0f);
				flowfield.DestroyLevelZero[i] = new float2(destroyLevelZeroBuilder.Values[i], 0f);
			}
			//

			foodLevelZeroBuilder.Dispose();
			destroyLevelZeroBuilder.Dispose();

			new SpawnerInitJob().ScheduleParallel(state.Dependency).Complete();

			state.EntityManager.AddComponentData(state.SystemHandle, partition);
			state.EntityManager.AddComponentData(state.SystemHandle, flowfield);
			state.Enabled = false;
		}

		[BurstCompile]
		public partial struct RoomInitJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			public Partition Partition;

			public void Execute(Entity entity, in PositionComponent position, in RoomComponent room)
			{
				for (int y = 0; y < room.Dimensions.y; y++)
				{
					for (int x = 0; x < room.Dimensions.x; x++)
					{
						Partition.SetCellData(
							true,
							new RoomData
							{
								Entity = entity,
								Position = position.Value,
								Room = room,
							},
							new float2(position.x + x - room.Dimensions.x / 2f, position.y + y - room.Dimensions.y / 2f));
					}
				}
			}
		}

		[BurstCompile]
		public partial struct RoomLinkJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			public Partition Partition;

			public void Execute(in RoomLinkComponent link)
			{
				// try to retreive the two connected rooms
				RoomData room1;
				RoomData room2;
				if (Partition.IsPathable(new float2(link.Position.x + 1, link.Position.y))) // horizontal
				{
					room1 = Partition.GetRoomData(new float2(link.Position.x - 1, link.Position.y));
					room2 = Partition.GetRoomData(new float2(link.Position.x + 1, link.Position.y));
				}
				else // vertical
				{
					room1 = Partition.GetRoomData(new float2(link.Position.x, link.Position.y - 1));
					room2 = Partition.GetRoomData(new float2(link.Position.x, link.Position.y + 1));
				}

				// room link is "owned" by the biggest room
				Partition.SetCellData(true, room1.Size >= room2.Size ? room1 : room2, link.Position);
			}
		}

		[BurstCompile]
		[WithAll(typeof(DeviceTag))]
		public partial struct DeviceFlowfieldInitJob : IJobEntity
		{
			[ReadOnly] public Partition Partition;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder FoodLevelZeroBuilder;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder DestroyLevelZeroBuilder;

			public void Execute(in InteractableComponent interactable, in LocalTransform transform)
			{
				// TODO check partition to know autorisation level (later)

				// we assume normalized scale
				float2 position = new float2(transform.Position.x, transform.Position.z);
				int size = (int)transform.Scale;

				FoodLevelZeroBuilder.ProcessDevice(in interactable, in Partition, position, size);
				DestroyLevelZeroBuilder.ProcessDevice(in interactable, in Partition, position, size);
			}
		}

		[BurstCompile]
		public partial struct SpawnerInitJob : IJobEntity
		{
			public void Execute(ref InteractableComponent interactable, in SpawnerComponent spawner)
			{
				// remove collect flag
				// (was needed for flowfield init)
				if (spawner.Capacity == 0)
				{
					interactable.ActionFlags &= ~ActionFlag.Collect;
				}
			}
		}

		[BurstCompile]
		public partial struct FlowfieldJob : IJob
		{
			[ReadOnly]
			public Partition Partition;
			[ReadOnly]
			public NativeArray<int> values;
			public NativeArray<float2> flowfield;
			
			public void Execute()
			{

			}
		}
	}
}