using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	public partial struct ActionSystem : ISystem
	{
		private BufferLookup<PickDropEventBufferElement> _pickDropEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<PickComponent> _pickLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<PartitionComponent> _partitionLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PickDropEventBufferElement>();
			state.RequireForUpdate<RoomPartition>();

			_pickDropEventLookup = state.GetBufferLookup<PickDropEventBufferElement>();
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>();
			_pickLookup = state.GetComponentLookup<PickComponent>();
			_pickableLookup = state.GetComponentLookup<PickableComponent>();
			_positionLookup = state.GetComponentLookup<PositionComponent>();
			_partitionLookup = state.GetComponentLookup<PartitionComponent>();
			_interactableLookup = state.GetComponentLookup<InteractableComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_pickDropEventLookup.Update(ref state);
			_roomElementLookup.Update(ref state);
			_pickLookup.Update(ref state);
			_pickableLookup.Update(ref state);
			_positionLookup.Update(ref state);
			_partitionLookup.Update(ref state);
			_interactableLookup.Update(ref state);

			// TODO: verify // between different action processing jobs (avoid dependencies)
			// TBD: use Ecb/events in case of conflicts

			state.Dependency = new PickDropEventsJob
			{
				LookupEntity = SystemAPI.GetSingletonEntity<PickDropEventBufferElement>(),
				Partition = SystemAPI.GetSingleton<RoomPartition>(),
				ActionLookup = _pickDropEventLookup,
				RoomElementLookup = _roomElementLookup,
				PickLookup = _pickLookup,
				PickableLookup = _pickableLookup,
				PositionLookup = _positionLookup,
				PartitionLookup = _partitionLookup,
				InteractableLookup = _interactableLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct PickDropEventsJob : IJob
		{
			public Entity LookupEntity;
			public BufferLookup<PickDropEventBufferElement> ActionLookup;
			public BufferLookup<RoomElementBufferElement> RoomElementLookup;
			public ComponentLookup<PickComponent> PickLookup;
			public ComponentLookup<PickableComponent> PickableLookup;
			public ComponentLookup<PositionComponent> PositionLookup;
			public ComponentLookup<PartitionComponent> PartitionLookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup;
			[ReadOnly]
			public RoomPartition Partition;

			public void Execute()
			{
				DynamicBuffer<PickDropEventBufferElement> actions = ActionLookup[LookupEntity];
				using (var enumerator = actions.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						PickDropEventBufferElement actionEvent = enumerator.Current;

						// verify target has not been picked by another event
						if (actionEvent.Action.Type == ActionType.Pick && !PickableLookup.IsComponentEnabled(actionEvent.Action.Target))
						{
							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = actionEvent.Action.Target;
							PickableLookup.GetRefRW(actionEvent.Action.Target).ValueRW.Carrier = actionEvent.Source;
							PickLookup.SetComponentEnabled(actionEvent.Source, true);
							PickableLookup.SetComponentEnabled(actionEvent.Action.Target, true);

							Entity room = Partition.GetRoom(actionEvent.Action.Position);
							if (room != Entity.Null)
							{
								DynamicBuffer<RoomElementBufferElement> roomElements = RoomElementLookup[room];
								RoomElementBufferElement.RemoveElement(ref roomElements, new RoomElementBufferElement(actionEvent.Action.Target, actionEvent.Action.Position, InteractableLookup[actionEvent.Action.Target].Flags));
								PartitionLookup.GetRefRW(actionEvent.Action.Target).ValueRW.CurrentRoom = Entity.Null;
							}
						}
						else if (actionEvent.Action.Type == ActionType.Drop)
						{
							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = Entity.Null;
							PickableLookup.GetRefRW(actionEvent.Action.Target).ValueRW.Carrier = Entity.Null;
							PickLookup.SetComponentEnabled(actionEvent.Source, false);
							PickableLookup.SetComponentEnabled(actionEvent.Action.Target, false);
							PositionLookup.GetRefRW(actionEvent.Action.Target).ValueRW.Value = actionEvent.Action.Position;

							Entity room = Partition.GetRoom(actionEvent.Action.Position);
							if (room != Entity.Null)
							{
								RoomElementLookup[room].Add(new RoomElementBufferElement(actionEvent.Action.Target, actionEvent.Action.Position, InteractableLookup[actionEvent.Action.Target].Flags));
								PartitionLookup.GetRefRW(actionEvent.Action.Target).ValueRW.CurrentRoom = room;
							}
						}
					}
				}
				actions.Clear();
			}
		}
	}
}