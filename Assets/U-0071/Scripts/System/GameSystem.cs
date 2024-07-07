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

			new PartitionInitJob
			{
				Partition = partition,
			}.ScheduleParallel(state.Dependency).Complete();

			// TODO: room links init

			state.EntityManager.AddComponentData(state.SystemHandle, partition);
			state.Enabled = false;
		}

		[BurstCompile]
		public partial struct PartitionInitJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			public RoomPartition Partition;

			public void Execute(Entity entity, in PositionComponent position, in RoomComponent room)
			{
				for (int y = 0; y < room.Dimensions.y; y++)
				{
					for (int x = 0; x < room.Dimensions.x; x++)
					{
						Partition.SetRoomData(
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
	}
}