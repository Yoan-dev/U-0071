using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	public partial struct PlayerInputSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			ref PlayerController controller = ref SystemAPI.GetSingletonRW<PlayerController>().ValueRW;

			controller.MoveInput = math.normalizesafe(new float2
			{
				x = Input.GetKey(KeyCode.D) ? 1f : Input.GetKey(KeyCode.A) ? -1f : 0f,
				y = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f,
			});

			if (Input.GetKeyDown(controller.FirstInteraction.Key))
			{
				controller.FirstInteraction.IsPressed = true;
			}
			if (Input.GetKeyDown(controller.SecondInteraction.Key))
			{
				controller.SecondInteraction.IsPressed = true;
			}

			// TODO
			controller.LookInput = new float2();
		}
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct PlayerControllerSystem : ISystem
	{
		private BufferLookup<ActionEventBufferElement> _actionEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<NameComponent> _nameLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();

			_actionEventLookup = state.GetBufferLookup<ActionEventBufferElement>();
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_nameLookup = state.GetComponentLookup<NameComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_actionEventLookup.Update(ref state);
			_roomElementLookup.Update(ref state);
			_nameLookup.Update(ref state);

			state.Dependency = new PlayerMovementJob().Schedule(state.Dependency);

			state.Dependency = new PlayerActionJob
			{
				LookupEntity = SystemAPI.GetSingletonEntity<ActionEventBufferElement>(),
				ActionEventBufferLookup = _actionEventLookup,
				RoomElementBufferLookup = _roomElementLookup,
				NameLookup = _nameLookup,
			}.Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct PlayerMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PlayerController controller)
			{
				movement.Input = controller.MoveInput;
			}
		}

		[BurstCompile]
		public partial struct PlayerActionJob : IJobEntity
		{
			public Entity LookupEntity;
			public BufferLookup<ActionEventBufferElement> ActionEventBufferLookup;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<NameComponent> NameLookup;

			public void Execute(Entity entity, ref PlayerController controller, in PositionComponent position, in PickComponent pick, in PartitionComponent partition)
			{
				if (partition.CurrentRoom == Entity.Null) return;

				controller.FirstInteraction.Type = 0;
				controller.SecondInteraction.Type = 0;
				Entity firstTarget = Entity.Null;
				Entity secondTarget = Entity.Null;

				// we boldly assume that all partitioned elements have an interactable and a name component

				if (pick.Picked != Entity.Null)
				{
					secondTarget = pick.Picked;
					controller.SecondInteraction.Name = NameLookup[pick.Picked].Value;
					controller.SecondInteraction.Type = ActionType.Drop;
				}

				if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, 0, out RoomElementBufferElement target))
				{
					if (math.lengthsq(position.Value - target.Position) <= math.pow(Const.InteractionRange, 2f))
					{
						if (controller.SecondInteraction.Type != ActionType.Drop && 
							Utilities.IsActionType(target.ActionType, ActionType.Pick))
						{
							secondTarget = target.Element;
							controller.SecondInteraction.Name = NameLookup[target.Element].Value;
							controller.SecondInteraction.Type = ActionType.Pick;
						}
					}
				}

				// TODO: improve
				if (controller.FirstInteraction.IsPressed && firstTarget != Entity.Null)
				{
					ActionEventBufferLookup[LookupEntity].Add(new ActionEventBufferElement
					{
						Source = entity,
						Target = firstTarget,
						Type = controller.FirstInteraction.Type,
					});
				}
				if (controller.SecondInteraction.IsPressed && secondTarget != Entity.Null)
				{
					ActionEventBufferLookup[LookupEntity].Add(new ActionEventBufferElement
					{
						Source = entity,
						Target = secondTarget,
						Type = controller.SecondInteraction.Type,
					});
				}

				// consume inputs
				controller.FirstInteraction.IsPressed = false;
				controller.SecondInteraction.IsPressed = false;
			}
		}
	}
}