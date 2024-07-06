using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial struct DebugSystem : ISystem
	{
		public struct RoomInfo
		{
			public FixedString32Bytes Name;
			public float2 Position;
			public Entity Entity;
			public int ElementCount;
		}

		private EntityQuery _roomQuery;
		private bool _debugRooms;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			//state.Enabled = false;

			_roomQuery = SystemAPI.QueryBuilder()
				.WithAll<PositionComponent, NameComponent, RoomElementBufferElement>()
				.Build();
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
			{
				_debugRooms = !_debugRooms;

				if (!_debugRooms)
				{
					DebugManager.Instance.ClearRoomElements();
				}
			}
			if (_debugRooms)
			{
				NativeList<RoomInfo> roomInfos = new NativeList<RoomInfo>(_roomQuery.CalculateEntityCount(), Allocator.TempJob);

				new RoomInfoCollectorJob
				{
					RoomInfos = roomInfos.AsParallelWriter(),
				}.ScheduleParallel(_roomQuery, state.Dependency).Complete();

				DebugManager.Instance.UpdateRoomElements(in roomInfos);
				roomInfos.Dispose();
			}
		}

		[BurstCompile]
		public partial struct RoomInfoCollectorJob : IJobEntity
		{
			[WriteOnly]
			public NativeList<RoomInfo>.ParallelWriter RoomInfos;

			public void Execute(Entity entity, in PositionComponent position, in NameComponent name, in DynamicBuffer<RoomElementBufferElement> elements)
			{
				// TODO: room link infos

				RoomInfos.AddNoResize(new RoomInfo
				{
					Entity = entity,
					Name = name.Value,
					Position = position.Value,
					ElementCount = elements.Length,
				});
			}
		}
	}
}