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

			if (Input.GetKeyDown(controller.PrimaryAction.Key))
			{
				controller.PrimaryAction.IsPressed = true;
			}
			if (Input.GetKeyDown(controller.SecondaryAction.Key))
			{
				controller.SecondaryAction.IsPressed = true;
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
			state.RequireForUpdate<ActionEventBufferElement>();

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
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct PlayerActionJob : IJobEntity
		{
			public Entity LookupEntity;
			public BufferLookup<ActionEventBufferElement> ActionEventBufferLookup;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<NameComponent> NameLookup;

			public void Execute(
				Entity entity,
				ref PlayerController controller,
				ref Orientation orientation,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition,
				in CreditsComponent credits)
			{
				// reset
				controller.PrimaryAction.Reset();
				controller.SecondaryAction.Reset();

				// cannot act if not in partition
				if (partition.CurrentRoom == Entity.Null) return;

				// we boldly assume that all partitioned elements have an interactable and a name component

				// carry item actions
				if (pick.Picked != Entity.Null)
				{
					controller.SetSecondaryAction(new ActionData(pick.Picked, ActionType.Drop, position.Value + new float2(Const.DropItemOffset.x * orientation.Value, Const.DropItemOffset.y), 0f, 0), in NameLookup, pick.Picked);
				}

				// close room items actions
				if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, ActionType.All, out RoomElementBufferElement target) &&
					position.IsInRange(target.Position, target.Range))
				{
					// TODO: check !controller.HasPrimaryAction for subsequent actions
					if (pick.Picked != Entity.Null && target.HasActionType(ActionType.Grind))
					{
						controller.SetPrimaryAction(target.ToActionData(ActionType.Grind), in NameLookup, pick.Picked);
					}
					else if (!controller.HasSecondaryAction && target.HasActionType(ActionType.Pick))
					{
						controller.SetSecondaryAction(target.ToActionData(ActionType.Pick), in NameLookup, pick.Picked);
					}
				}

				// queue action if needed/able
				if (controller.PrimaryAction.IsPressed && controller.HasPrimaryAction &&
					(controller.PrimaryTarget.Cost == 0 || controller.PrimaryTarget.Cost <= credits.Value))
				{
					ActionEventBufferLookup[LookupEntity].Add(new ActionEventBufferElement
					{
						Source = entity,
						Action = controller.PrimaryTarget,
					});
					orientation.Update(controller.PrimaryTarget.Position.x - position.x);
				}
				else if (
					controller.SecondaryAction.IsPressed && controller.HasSecondaryAction && 
					(controller.SecondaryTarget.Cost == 0 || controller.SecondaryTarget.Cost <= credits.Value))
				{
					ActionEventBufferLookup[LookupEntity].Add(new ActionEventBufferElement
					{
						Source = entity,
						Action = controller.SecondaryTarget,
					});
					orientation.Update(controller.SecondaryTarget.Position.x - position.x);
				}

				// consume inputs
				controller.PrimaryAction.IsPressed = false;
				controller.SecondaryAction.IsPressed = false;
			}
		}
	}
}