using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static UnityEngine.GraphicsBuffer;

namespace U0071
{

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		private BufferLookup<PickDropEventBufferElement> _pickDropEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private float _timer;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PickDropEventBufferElement>();

			_pickDropEventLookup = state.GetBufferLookup<PickDropEventBufferElement>();
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_interactableLookup = state.GetComponentLookup<InteractableComponent>(true);
			_pickableLookup = state.GetComponentLookup<PickableComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// TODO: try batching
			_timer += SystemAPI.Time.DeltaTime;
			if (_timer >= Const.AITick)
			{
				_timer -= Const.AITick;

				_pickDropEventLookup.Update(ref state);
				_roomElementLookup.Update(ref state);
				_interactableLookup.Update(ref state);
				_pickableLookup.Update(ref state);

				state.Dependency = new AIActionJob
				{
					RoomElementBufferLookup = _roomElementLookup,
					InteractableLookup = _interactableLookup,
					PickableLookup = _pickableLookup,
				}.ScheduleParallel(state.Dependency);

				state.Dependency = new AIMovementJob().ScheduleParallel(state.Dependency);
			}
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct AIActionJob : IJobEntity
		{
			// TODO: native list or wrapper for action events
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup; // to check destruction
			[ReadOnly]
			public ComponentLookup<PickableComponent> PickableLookup;

			public void Execute(
				Entity entity,
				ref AIController controller,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition)
			{
				if (partition.CurrentRoom == Entity.Null) return;

				// TODO: consider picked interactable action

				// make sure target is still avaialable (not destroyed or picked)
				if (controller.HasTarget &&
					(!PickableLookup.HasComponent(controller.Target.Target) || !PickableLookup.IsComponentEnabled(controller.Target.Target)) &&
					InteractableLookup.HasComponent(controller.Target.Target))
				{
					if (position.IsInActionRange(controller.Target.Position))
					{
						// TODO
					}
				}
				else
				{
					// reset if picked / destroyed
					controller.Target.Target = Entity.Null;

					// retrieve relevant action types
					ActionType filter = ActionType.All;

					if (pick.Picked != Entity.Null)
					{
						filter &= ~ActionType.Pick;
					}

					// look for target
					if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, filter, out RoomElementBufferElement target))
					{
						// consider which action to do in priority
						// TBD: improve
						ActionType actionType = 0;
						if (Utilities.HasActionType(target.ActionType, ActionType.Pick))
						{
							actionType = ActionType.Pick;
						}
						else if (Utilities.HasActionType(target.ActionType, ActionType.Use))
						{
							actionType = ActionType.Use;
						}

						if (actionType != 0)
						{
							if (position.IsInActionRange(target.Position))
							{
								// interact
								// TODO
							}
							else
							{
								// track
								controller.Target = new ActionTarget(target.Entity, actionType, target.Position);
							}
						}
						
					}
				}
			}
		}

		[BurstCompile]
		public partial struct AIMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PositionComponent position, in AIController controller)
			{
				if (controller.HasTarget)
				{
					float2 direction = controller.Target.Position - position.Value;
					movement.Input = math.lengthsq(direction) > math.pow(Const.ActionRange, 2f) ? math.normalize(direction) : float2.zero;
				}
				else
				{
					movement.Input = float2.zero;
				}
			}
		}

	}
}