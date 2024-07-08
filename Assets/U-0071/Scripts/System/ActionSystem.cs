using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct ActionSystem : ISystem
	{
		// TODO: accessible native list/queue instead of collector thingy

		private BufferLookup<ActionEventBufferElement> _actionEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<PickComponent> _pickLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<PartitionComponent> _partitionLookup;
		private ComponentLookup<CreditsComponent> _creditsLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<ActionEventBufferElement>();
			state.RequireForUpdate<RoomPartition>();

			_actionEventLookup = state.GetBufferLookup<ActionEventBufferElement>();
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>();
			_pickLookup = state.GetComponentLookup<PickComponent>();
			_pickableLookup = state.GetComponentLookup<PickableComponent>();
			_positionLookup = state.GetComponentLookup<PositionComponent>();
			_partitionLookup = state.GetComponentLookup<PartitionComponent>();
			_creditsLookup = state.GetComponentLookup<CreditsComponent>();
			_interactableLookup = state.GetComponentLookup<InteractableComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			_actionEventLookup.Update(ref state);
			_roomElementLookup.Update(ref state);
			_pickLookup.Update(ref state);
			_pickableLookup.Update(ref state);
			_positionLookup.Update(ref state);
			_partitionLookup.Update(ref state);
			_creditsLookup.Update(ref state);
			_interactableLookup.Update(ref state);

			// TODO/TBD: split action event types depending on dependencies (parallel processing)

			state.Dependency = new ActionEventsJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged),
				LookupEntity = SystemAPI.GetSingletonEntity<ActionEventBufferElement>(),
				Partition = SystemAPI.GetSingleton<RoomPartition>(),
				ActionLookup = _actionEventLookup,
				RoomElementLookup = _roomElementLookup,
				PickLookup = _pickLookup,
				PickableLookup = _pickableLookup,
				PositionLookup = _positionLookup,
				PartitionLookup = _partitionLookup,
				CreditsLookup = _creditsLookup,
				InteractableLookup = _interactableLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct ActionEventsJob : IJob
		{
			public EntityCommandBuffer Ecb;
			public Entity LookupEntity;
			public BufferLookup<ActionEventBufferElement> ActionLookup;
			public BufferLookup<RoomElementBufferElement> RoomElementLookup;
			public ComponentLookup<PickComponent> PickLookup;
			public ComponentLookup<PickableComponent> PickableLookup;
			public ComponentLookup<PositionComponent> PositionLookup;
			public ComponentLookup<PartitionComponent> PartitionLookup;
			public ComponentLookup<CreditsComponent> CreditsLookup;
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

						// could be changed depending on action
						int cost = actionEvent.Action.Cost;

						if (actionEvent.Type == ActionType.Buy)
						{
							// TODO
						}
						else if (actionEvent.Type == ActionType.Grind)
						{
							ref PickComponent pick = ref PickLookup.GetRefRW(actionEvent.Source).ValueRW;
							Ecb.DestroyEntity(pick.Picked);
							pick.Picked = Entity.Null;
							PickLookup.SetComponentEnabled(actionEvent.Source, false);
						}
						else if (actionEvent.Type == ActionType.Pick)
						{
							// verify target has not been picked by another event
							if (PickableLookup.IsComponentEnabled(actionEvent.Target))
							{
								continue;
							}

							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = actionEvent.Target;
							PickLookup.SetComponentEnabled(actionEvent.Source, true);
							PickableLookup.GetRefRW(actionEvent.Target).ValueRW.Carrier = actionEvent.Source;
							PickableLookup.SetComponentEnabled(actionEvent.Target, true);

							Entity room = Partition.GetRoomData(actionEvent.Position).Entity;
							if (room != Entity.Null)
							{
								DynamicBuffer<RoomElementBufferElement> roomElements = RoomElementLookup[room];
								RoomElementBufferElement.RemoveElement(ref roomElements, new RoomElementBufferElement(actionEvent.Target));
								PartitionLookup.GetRefRW(actionEvent.Target).ValueRW.CurrentRoom = Entity.Null;
							}
						}
						else if (actionEvent.Type == ActionType.Drop)
						{
							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = Entity.Null;
							PickLookup.SetComponentEnabled(actionEvent.Source, false);
							PickableLookup.GetRefRW(actionEvent.Target).ValueRW.Carrier = Entity.Null;
							PickableLookup.SetComponentEnabled(actionEvent.Target, false);

							ref PositionComponent position = ref PositionLookup.GetRefRW(actionEvent.Target).ValueRW;
							position.Value = actionEvent.Action.Position;
							position.BaseYOffset = Const.ItemYOffset;
						}

						if (cost != 0f)
						{
							// can be negative (task reward)
							CreditsLookup.GetRefRW(actionEvent.Source).ValueRW.Value -= cost;
						}
					}
				}
				actions.Clear();
			}
		}
	}
}