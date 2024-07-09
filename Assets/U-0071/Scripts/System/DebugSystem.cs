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

		public struct FlowfieldInfo
		{
			public float2 Position;
			public float2 Value;
			public int Index;
		}

		private EntityQuery _query;
		private bool _debugRooms;
		private bool _debugFoodLevelZero;
		private bool _debugDestroyLevelZero;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			//state.Enabled = false;

			_query = SystemAPI.QueryBuilder()
				.WithAll<PositionComponent, NameComponent, RoomElementBufferElement>()
				.Build();
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
			{
				_debugRooms = !_debugRooms;
				if (!_debugRooms) DebugManager.Instance.ClearRoomElements();
			}

			if (_debugRooms)
			{
				NativeList<RoomInfo> roomInfos = new NativeList<RoomInfo>(_query.CalculateEntityCount(), Allocator.TempJob);

				new RoomInfoCollectorJob
				{
					RoomInfos = roomInfos.AsParallelWriter(),
				}.ScheduleParallel(_query, state.Dependency).Complete();

				DebugManager.Instance.UpdateRoomElements(in roomInfos);
				roomInfos.Dispose();
			}

			if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha1))
			{
				_debugFoodLevelZero = !_debugFoodLevelZero;
				if (_debugFoodLevelZero)
				{
					Flowfield flowfield = SystemAPI.GetSingleton<Flowfield>();
					DebugFlowfield(in flowfield.FoodLevelZero, flowfield.Dimensions);
				}
				else DebugManager.Instance.ClearFlowfieldElements();
			}
			if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Alpha2))
			{
				_debugDestroyLevelZero = !_debugDestroyLevelZero;
				if (_debugDestroyLevelZero)
				{
					Flowfield flowfield = SystemAPI.GetSingleton<Flowfield>();
					DebugFlowfield(in flowfield.DestroyLevelZero, flowfield.Dimensions);
				}
				else DebugManager.Instance.ClearFlowfieldElements();
			}
		}

		private void DebugFlowfield(in NativeArray<float2> flowfield, float2 dimensions)
		{
			Partition partition = SystemAPI.GetSingleton<Partition>();
			NativeArray<FlowfieldInfo> flowfieldInfos = new NativeArray<FlowfieldInfo>(flowfield.Length, Allocator.TempJob);

			for (int i = 0; i < flowfield.Length; i++)
			{
				if (partition.IsPathable(i))
				{
					flowfieldInfos[i] = new FlowfieldInfo
					{
						Index = i,
						Position = new float2(i % dimensions.x - dimensions.x / 2f + 0.5f, i / dimensions.x - dimensions.y / 2f),
						Value = flowfield[i],
					};
				}
			}
			
			DebugManager.Instance.UpdateFlowfieldElements(in flowfieldInfos);
			flowfieldInfos.Dispose();
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