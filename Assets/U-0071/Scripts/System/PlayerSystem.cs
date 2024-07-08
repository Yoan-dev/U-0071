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
				in PartitionComponent partition)
			{
				// reset
				controller.PrimaryTarget.Target = Entity.Null;
				controller.SecondaryTarget.Target = Entity.Null;
				controller.PrimaryInfo.Type = 0;
				controller.SecondaryInfo.Type = 0;

				// cannot act if not in partition
				if (partition.CurrentRoom == Entity.Null) return;

				// we boldly assume that all partitioned elements have an interactable and a name component

				if (pick.Picked != Entity.Null)
				{
					controller.SetSecondaryAction(new ActionData(pick.Picked, ActionType.Drop, position.Value + new float2(Const.DropItemOffset.x * orientation.Value, Const.DropItemOffset.y), 0f, 0), in NameLookup, ActionType.Drop);
				}

				if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, ActionType.All, out RoomElementBufferElement target) &&
					position.IsInRange(target.Position, target.Range))
				{
					// TODO: check !controller.HasPrimaryAction for subsequent actions
					if (pick.Picked != Entity.Null && target.HasActionType(ActionType.Grind))
					{
						controller.SetPrimaryAction(in target, in NameLookup, ActionType.Grind);
					}
					else if (!controller.HasSecondaryAction && target.HasActionType(ActionType.Pick))
					{
						controller.SetSecondaryAction(in target, in NameLookup, ActionType.Pick);
					}
				}

				if (controller.PrimaryInfo.IsPressed && controller.HasPrimaryAction)
				{
					ActionEventBufferLookup[LookupEntity].Add(new ActionEventBufferElement
					{
						Source = entity,
						Action = controller.PrimaryTarget,
					});
					orientation.Update(controller.PrimaryTarget.Position.x - position.x);
				}
				else if (controller.SecondaryInfo.IsPressed && controller.HasSecondaryAction)
				{
					ActionEventBufferLookup[LookupEntity].Add(new ActionEventBufferElement
					{
						Source = entity,
						Action = controller.SecondaryTarget,
					});
					orientation.Update(controller.SecondaryTarget.Position.x - position.x);
				}

				// consume inputs
				controller.PrimaryInfo.IsPressed = false;
				controller.SecondaryInfo.IsPressed = false;
			}
		}
	}
}