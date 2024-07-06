using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	public partial struct ActionSystem : ISystem
	{
		private BufferLookup<ActionEventBufferElement> _actionEventLookup;
		private ComponentLookup<PickComponent> _pickLookup;
		private ComponentLookup<PickedTag> _pickedLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<ActionEventBufferElement>();

			_actionEventLookup = state.GetBufferLookup<ActionEventBufferElement>();
			_pickLookup = state.GetComponentLookup<PickComponent>();
			_pickedLookup = state.GetComponentLookup<PickedTag>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_actionEventLookup.Update(ref state);
			_pickLookup.Update(ref state);
			_pickedLookup.Update(ref state);

			// TODO: specialized action event jobs processed by type (depending on dependencies, verify //)

			state.Dependency = new ActionEventsJob
			{
				LookupEntity = SystemAPI.GetSingletonEntity<ActionEventBufferElement>(),
				ActionLookup = _actionEventLookup,
				PickLookup = _pickLookup,
				PickedLookup = _pickedLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct ActionEventsJob : IJob
		{
			public Entity LookupEntity;
			public BufferLookup<ActionEventBufferElement> ActionLookup;
			public ComponentLookup<PickComponent> PickLookup;
			public ComponentLookup<PickedTag> PickedLookup;

			public void Execute()
			{
				DynamicBuffer<ActionEventBufferElement> actions = ActionLookup[LookupEntity];
				using (var enumerator = actions.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						ActionEventBufferElement actionEvent = enumerator.Current;

						if (actionEvent.Type == ActionType.Pick)
						{
							// verify no one else took the pickable
							if (!PickedLookup.IsComponentEnabled(actionEvent.Target))
							{
								PickedLookup.SetComponentEnabled(actionEvent.Target, true);
								PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = actionEvent.Target;
							}
						}
						else
						{
							// TBD: verify target is still picked (dropped by another system ?)
							PickedLookup.SetComponentEnabled(actionEvent.Target, false);
							PickLookup.GetRefRW(actionEvent.Source).ValueRW.Picked = Entity.Null;
						}
					}
				}
				actions.Clear();
			}
		}
	}
}