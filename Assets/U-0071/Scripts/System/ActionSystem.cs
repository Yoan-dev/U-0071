using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	public partial struct ActionSystem : ISystem
	{
		private BufferLookup<ActionEventBufferElement> _actionEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<PickComponent> _pickLookup;
		private ComponentLookup<PickedComponent> _pickedLookup;
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<PartitionComponent> _partitionLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<ActionEventBufferElement>();
			state.RequireForUpdate<RoomPartition>();

			_actionEventLookup = state.GetBufferLookup<ActionEventBufferElement>();
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>();
			_pickLookup = state.GetComponentLookup<PickComponent>();
			_pickedLookup = state.GetComponentLookup<PickedComponent>();
			_positionLookup = state.GetComponentLookup<PositionComponent>();
			_partitionLookup = state.GetComponentLookup<PartitionComponent>();
			_interactableLookup = state.GetComponentLookup<InteractableComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_actionEventLookup.Update(ref state);
			_roomElementLookup.Update(ref state);
			_pickLookup.Update(ref state);
			_pickedLookup.Update(ref state);
			_positionLookup.Update(ref state);
			_partitionLookup.Update(ref state);
			_interactableLookup.Update(ref state);

			// TODO: specialized action event jobs processed by type (depending on dependencies, verify //)

			state.Dependency = new ActionEventsJob
			{
				LookupEntity = SystemAPI.GetSingletonEntity<ActionEventBufferElement>(),
				Partition = SystemAPI.GetSingleton<RoomPartition>(),
				ActionLookup = _actionEventLookup,
				RoomElementLookup = _roomElementLookup,
				PickLookup = _pickLookup,
				PickedLookup = _pickedLookup,
				PositionLookup = _positionLookup,
				PartitionLookup = _partitionLookup,
				InteractableLookup = _interactableLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct ActionEventsJob : IJob
		{
			public Entity LookupEntity;
			public BufferLookup<ActionEventBufferElement> ActionLookup;
			public BufferLookup<RoomElementBufferElement> RoomElementLookup;
			public ComponentLookup<PickComponent> PickLookup;
			public ComponentLookup<PickedComponent> PickedLookup;
			public ComponentLookup<PositionComponent> PositionLookup;
			public ComponentLookup<PartitionComponent> PartitionLookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup;
			[ReadOnly]
			public RoomPartition Partition;

			public void Execute()
			{
				DynamicBuffer<ActionEventBufferElement> actions = ActionLookup[LookupEntity];
				using (var enumerator = actions.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						ActionEventBufferElement actionEvent = enumerator.Current;

						// verify target has not been picked
						if (actionEvent.Type == ActionType.Pick && !PickedLookup.IsComponentEnabled(actionEvent.Target))
						{
							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = actionEvent.Target;
							PickedLookup.GetRefRW(actionEvent.Target).ValueRW.Carrier = actionEvent.Source;
							PickLookup.SetComponentEnabled(actionEvent.Source, true);
							PickedLookup.SetComponentEnabled(actionEvent.Target, true);

							Entity room = Partition.GetRoom(actionEvent.Position);
							if (room != Entity.Null)
							{
								DynamicBuffer<RoomElementBufferElement> roomElements = RoomElementLookup[room];
								RoomElementBufferElement.RemoveElement(ref roomElements, new RoomElementBufferElement(actionEvent.Target, actionEvent.Position, InteractableLookup[actionEvent.Target].Type));
								PartitionLookup.GetRefRW(actionEvent.Target).ValueRW.CurrentRoom = Entity.Null;
							}
						}
						else if (actionEvent.Type == ActionType.Drop)
						{
							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = Entity.Null;
							PickedLookup.GetRefRW(actionEvent.Target).ValueRW.Carrier = Entity.Null;
							PickLookup.SetComponentEnabled(actionEvent.Source, false);
							PickedLookup.SetComponentEnabled(actionEvent.Target, false);
							PositionLookup.GetRefRW(actionEvent.Target).ValueRW.Value = actionEvent.Position;

							Entity room = Partition.GetRoom(actionEvent.Position);
							if (room != Entity.Null)
							{
								RoomElementLookup[room].Add(new RoomElementBufferElement(actionEvent.Target, actionEvent.Position, InteractableLookup[actionEvent.Target].Type));
								PartitionLookup.GetRefRW(actionEvent.Target).ValueRW.CurrentRoom = room;
							}
						}
					}
				}
				actions.Clear();
			}
		}
	}
}