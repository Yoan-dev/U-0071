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
		private BufferLookup<PickDropEventBufferElement> _pickDropEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<NameComponent> _nameLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();
			state.RequireForUpdate<PickDropEventBufferElement>();

			_pickDropEventLookup = state.GetBufferLookup<PickDropEventBufferElement>();
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_nameLookup = state.GetComponentLookup<NameComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_pickDropEventLookup.Update(ref state);
			_roomElementLookup.Update(ref state);
			_nameLookup.Update(ref state);

			state.Dependency = new PlayerMovementJob().Schedule(state.Dependency);

			state.Dependency = new PlayerActionJob
			{
				LookupEntity = SystemAPI.GetSingletonEntity<PickDropEventBufferElement>(),
				PickDropEventBufferLookup = _pickDropEventLookup,
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
			public BufferLookup<PickDropEventBufferElement> PickDropEventBufferLookup;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<NameComponent> NameLookup;

			public void Execute(
				Entity entity,
				ref PlayerController controller,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition)
			{
				if (partition.CurrentRoom == Entity.Null) return;

				// reset
				controller.Primary.Target = Entity.Null;
				controller.Secondary.Target = Entity.Null;
				controller.PrimaryInfo.Type = 0;
				controller.SecondaryInfo.Type = 0;

				// we boldly assume that all partitioned elements have an interactable and a name component

				if (pick.Picked != Entity.Null)
				{
					controller.Secondary = new ActionTarget(pick.Picked, ActionType.Drop, position.Value);
					controller.SecondaryInfo.Name = NameLookup[pick.Picked].Value;
					controller.SecondaryInfo.Type = ActionType.Drop;
				}

				if (Utilities.GetClosestRoomElement(RoomElementBufferLookup[partition.CurrentRoom], position.Value, entity, ActionType.All, out RoomElementBufferElement target) &&
					position.IsInActionRange(target.Position))
				{
					if (!controller.HasSecondaryAction && Utilities.HasActionType(target.ActionType, ActionType.Pick))
					{
						controller.Secondary = new ActionTarget(target.Entity, ActionType.Pick, target.Position);
						controller.SecondaryInfo.Name = NameLookup[target.Entity].Value;
						controller.SecondaryInfo.Type = ActionType.Pick;
					}
				}

				if (controller.PrimaryInfo.IsPressed && controller.HasPrimaryAction)
				{
					PickDropEventBufferLookup[LookupEntity].Add(new PickDropEventBufferElement
					{
						Source = entity,
						Action = controller.Primary,
					});
				}
				if (controller.SecondaryInfo.IsPressed && controller.HasSecondaryAction)
				{
					PickDropEventBufferLookup[LookupEntity].Add(new PickDropEventBufferElement
					{
						Source = entity,
						Action = controller.Secondary,
					});
				}

				// consume inputs
				controller.PrimaryInfo.IsPressed = false;
				controller.SecondaryInfo.IsPressed = false;
			}
		}
	}
}