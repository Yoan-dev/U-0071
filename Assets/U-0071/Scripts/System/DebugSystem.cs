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
			public int Population;
			public int Capacity;
		}

		public struct FlowfieldInfo
		{
			public float2 Position;
			public float2 ToDestroyAndRed;
			public float2 Food;
			public float2 WorkdAndBlue;
			public float2 ProcessAndYellow;
			public float2 Wander;
			public float2 Relax;
		}

		private EntityQuery _query;
		private bool _debugRooms;
		private bool _debugLevelZero;
		private bool _debugLevelTwo;
		private bool _debugLevelThree;
		private bool _debugAdmin;
		private bool _debugCycle;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			//state.Enabled = false;

			_query = SystemAPI.QueryBuilder()
				.WithAll<PositionComponent, NameComponent, RoomElementBufferElement, RoomComponent>()
				.Build();
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// rooms
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

			// cycle
			if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
			{
				_debugCycle = !_debugCycle;
				if (!_debugCycle) DebugManager.Instance.ClearCycleElement();
			}
			if (_debugCycle)
			{
				DebugManager.Instance.UpdateCycleElement(SystemAPI.GetSingleton<CycleComponent>());
			}

			// flowfield
			if (CheckDebugFlowfield(KeyCode.Alpha1, ref _debugLevelZero))
			{
				FlowfieldCollection flowfieldCollection = SystemAPI.GetSingleton<FlowfieldCollection>();
				DebugFlowfield(in flowfieldCollection.LevelOne);
			}
			if (CheckDebugFlowfield(KeyCode.Alpha2, ref _debugLevelTwo))
			{
				FlowfieldCollection flowfieldCollection = SystemAPI.GetSingleton<FlowfieldCollection>();
				DebugFlowfield(in flowfieldCollection.LevelTwo);
			}
			if (CheckDebugFlowfield(KeyCode.Alpha3, ref _debugLevelThree))
			{
				FlowfieldCollection flowfieldCollection = SystemAPI.GetSingleton<FlowfieldCollection>();
				DebugFlowfield(in flowfieldCollection.LevelThree);
			}
			if (CheckDebugFlowfield(KeyCode.Alpha4, ref _debugAdmin))
			{
				DebugAdminFlowfield();
			}
		}

		private bool CheckDebugFlowfield(KeyCode key, ref bool value)
		{
			if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(key))
			{
				value = !value;
				if (!value)
				{
					DebugManager.Instance.ClearFlowfieldElements();
				}
				return value;
			}
			return false;
		}

		private void DebugFlowfield(in Flowfield flowfield)
		{
			Partition partition = SystemAPI.GetSingleton<Partition>();
			NativeArray<FlowfieldInfo> flowfieldInfos = new NativeArray<FlowfieldInfo>(flowfield.Cells.Length, Allocator.TempJob);
			for (int i = 0; i < flowfield.Cells.Length; i++)
			{
				if (partition.IsPathable(i))
				{
					FlowfieldCell cell = flowfield.Cells[i];
					flowfieldInfos[i] = new FlowfieldInfo
					{
						Position = new float2(i % flowfield.Dimensions.x - flowfield.Dimensions.x / 2f + 0.5f, i / flowfield.Dimensions.x + 0.5f - flowfield.Dimensions.y / 2f),
						ToDestroyAndRed = cell.ToDestroy,
						Food = cell.ToFood,
						WorkdAndBlue = cell.ToWork,
						ProcessAndYellow = cell.ToProcess,
						Wander = cell.ToWander,
						Relax = cell.ToRelax,
					};
				}
			}
			DebugManager.Instance.UpdateFlowfieldElements(in flowfieldInfos);
			flowfieldInfos.Dispose();
		}

		private void DebugAdminFlowfield()
		{
			Partition partition = SystemAPI.GetSingleton<Partition>();
			FlowfieldCollection flowfieldCollection = SystemAPI.GetSingleton<FlowfieldCollection>();
			NativeArray<FlowfieldInfo> flowfieldInfos = new NativeArray<FlowfieldInfo>(flowfieldCollection.ToRedAdmin.Length, Allocator.TempJob);
			for (int i = 0; i < flowfieldCollection.ToRedAdmin.Length; i++)
			{
				if (partition.IsPathable(i))
				{
					flowfieldInfos[i] = new FlowfieldInfo
					{
						Position = new float2(i % partition.Dimensions.x - partition.Dimensions.x / 2f + 0.5f, i / partition.Dimensions.x + 0.5f - partition.Dimensions.y / 2f),
						ToDestroyAndRed = flowfieldCollection.ToRedAdmin[i],
						WorkdAndBlue = flowfieldCollection.ToBlueAdmin[i],
						ProcessAndYellow = flowfieldCollection.ToYellowAdmin[i],
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

			public void Execute(Entity entity, in RoomComponent room, in PositionComponent position, in NameComponent name, in DynamicBuffer<RoomElementBufferElement> elements)
			{
				// TODO: room link infos

				RoomInfos.AddNoResize(new RoomInfo
				{
					Entity = entity,
					Name = name.Value,
					Position = position.Value,
					ElementCount = elements.Length,
					Capacity = room.Capacity,
					Population = room.Population,
				});
			}
		}
	}
}