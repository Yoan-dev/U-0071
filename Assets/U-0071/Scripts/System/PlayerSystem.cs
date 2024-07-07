using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

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

			if (Input.GetKeyDown(controller.PrimaryInfo.Key))
			{
				controller.PrimaryInfo.IsPressed = true;
			}
			if (Input.GetKeyDown(controller.SecondaryInfo.Key))
			{
				controller.SecondaryInfo.IsPressed = true;
			}

			// TODO
			controller.LookInput = new float2();
		}
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct PlayerControllerSystem : ISystem
	{
		private BufferLookup<PickDropEventBufferElement> _actionEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<NameComponent> _nameLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();
			state.RequireForUpdate<PickDropEventBufferElement>();

			_actionEventLookup = state.GetBufferLookup<PickDropEventBufferElement>();
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
				LookupEntity = SystemAPI.GetSingletonEntity<PickDropEventBufferElement>(),
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
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct PlayerActionJob : IJobEntity
		{
			public Entity LookupEntity;
			public BufferLookup<PickDropEventBufferElement> ActionEventBufferLookup;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<NameComponent> NameLookup;

			public void Execute(
				Entity entity,
				ref PlayerController playerController,
				ref ActionController actionController,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition)
			{
				if (partition.CurrentRoom == Entity.Null) return;

				// reset
				actionController.Primary.Target = Entity.Null;
				actionController.Secondary.Target = Entity.Null;
				playerController.PrimaryInfo.Type = 0f;
				playerController.SecondaryInfo.Type = 0f;

				// we boldly assume that all partitioned elements have an interactable and a name component

				if (pick.Picked != Entity.Null)
				{
					actionController.Secondary = new ActionTarget(pick.Picked, ActionType.Drop, position.Value);
					playerController.SecondaryInfo.Name = NameLookup[pick.Picked].Value;
					playerController.SecondaryInfo.Type = ActionType.Drop;
				}

				if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, 0, out RoomElementBufferElement target) &&
					math.lengthsq(position.Value - target.Position) <= math.pow(Const.InteractionRange, 2f))
				{
					if (!actionController.HasSecondaryAction && Utilities.IsActionType(target.ActionType, ActionType.Pick))
					{
						actionController.Secondary = new ActionTarget(target.Entity, ActionType.Pick, target.Position);
						playerController.SecondaryInfo.Name = NameLookup[target.Entity].Value;
						playerController.SecondaryInfo.Type = ActionType.Pick;
					}
				}

				if (playerController.PrimaryInfo.IsPressed && actionController.HasPrimaryAction)
				{
					ActionEventBufferLookup[LookupEntity].Add(new PickDropEventBufferElement
					{
						Source = entity,
						Target = actionController.Primary,
					});
				}
				if (playerController.SecondaryInfo.IsPressed && actionController.HasSecondaryAction)
				{
					ActionEventBufferLookup[LookupEntity].Add(new PickDropEventBufferElement
					{
						Source = entity,
						Target = actionController.Secondary,
					});
				}

				// consume inputs
				playerController.PrimaryInfo.IsPressed = false;
				playerController.SecondaryInfo.IsPressed = false;
			}
		}
	}
}