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
				SystemAPI.GetComponent<Flowfield>(state.SystemHandle).Dispose();
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

			// do not use ActionFlag.Collect (not set if starting capacity != 0)
			FlowfieldBuilder foodLevelZeroBuilder = new FlowfieldBuilder(flowfield.FoodLevelZero, 0, ItemFlag.Food, in partition);
			FlowfieldBuilder workLevelZeroBuilder = new FlowfieldBuilder(flowfield.WorkLevelZero, 0, 0, in partition, true);
			FlowfieldBuilder destroyBuilder = new FlowfieldBuilder(flowfield.Destroy, ActionFlag.Destroy, ItemFlag.Trash, in partition);
			FlowfieldBuilder noWorkBuilder = new FlowfieldBuilder(flowfield.NoWork, 0, 0, in partition);

			new DeviceFlowfieldInitJob
			{
				Partition = partition,
				FoodLevelZeroBuilder = foodLevelZeroBuilder,
				WorkLevelZeroBuilder = workLevelZeroBuilder,
				DestroyBuilder = destroyBuilder,
				NoWorkBuilder = noWorkBuilder,
			}.ScheduleParallel(state.Dependency).Complete();

			state.Dependency = new FlowfieldSpreadJob { Builder = foodLevelZeroBuilder }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = workLevelZeroBuilder }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = destroyBuilder }.Schedule(state.Dependency);
			state.Dependency = new FlowfieldSpreadJob { Builder = noWorkBuilder }.Schedule(state.Dependency);

			state.Dependency = new FlowfieldDirectionJob { Builder = foodLevelZeroBuilder }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = workLevelZeroBuilder }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = destroyBuilder }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);
			state.Dependency = new FlowfieldDirectionJob { Builder = noWorkBuilder }.ScheduleParallel(size, Const.ParallelForCount, state.Dependency);

			state.Dependency.Complete();

			foodLevelZeroBuilder.Dispose();
			destroyBuilder.Dispose();
			workLevelZeroBuilder.Dispose();
			noWorkBuilder.Dispose();

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

				// TODO: owned by higher autorisation level (if closed)
				// TODO: owned by the one with working station (if any) or non-corridor, then
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
			[NativeDisableParallelForRestriction] public FlowfieldBuilder WorkLevelZeroBuilder;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder DestroyBuilder;
			[NativeDisableParallelForRestriction] public FlowfieldBuilder NoWorkBuilder;

			public void Execute(in InteractableComponent interactable, in LocalTransform transform)
			{
				// TODO check partition to know autorisation level (later)

				// we assume normalized scale
				float2 position = new float2(transform.Position.x, transform.Position.z);
				int size = (int)transform.Scale;

				FoodLevelZeroBuilder.ProcessDevice(in interactable, in Partition, position, size);
				DestroyBuilder.ProcessDevice(in interactable, in Partition, position, size);
				WorkLevelZeroBuilder.ProcessDevice(in interactable, in Partition, position, size);
				NoWorkBuilder.ProcessDevice(in interactable, in Partition, position, size);
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
}