using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
			if (SystemAPI.HasComponent<RoomPartition>(state.SystemHandle))
			{
				SystemAPI.GetComponent<RoomPartition>(state.SystemHandle).Dispose();
			}
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			Config config = SystemAPI.GetSingleton<Config>();
			RoomPartition partition = new RoomPartition(config.WorldDimensions);

			// jobs are run with NativeDisableParallelForRestriction on the partition
			// because rooms and doors/links should not be on top of one another
			// (isolated cell indexes)
			// (otherwise map is wrong)

			new RoomInitJob
			{
				Partition = partition,
			}.ScheduleParallel(state.Dependency).Complete();

			new RoomLinkJob
			{
				Partition = partition,
			}.ScheduleParallel(state.Dependency).Complete();

			state.EntityManager.AddComponentData(state.SystemHandle, partition);
			state.Enabled = false;
		}

		[BurstCompile]
		public partial struct RoomInitJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			public RoomPartition Partition;

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
							new float2
							{
								x = position.x + x - room.Dimensions.x / 2f,
								y = position.y + y - room.Dimensions.y / 2f,
							});
					}
				}
			}
		}

		[BurstCompile]
		public partial struct RoomLinkJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			public RoomPartition Partition;

			public void Execute(Entity entity, in RoomLinkComponent link)
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

				// we assume that the room data are eligible
				// (otherwise map is wrong)

				Partition.SetCellData(true, new RoomData(), link.Position);

				// TBD: set the cell room data as the biggest adjacent room
			}
		}
	}
}