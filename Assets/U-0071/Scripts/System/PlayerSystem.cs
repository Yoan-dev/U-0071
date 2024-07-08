using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.Rendering.FilterWindow;
using static UnityEngine.GraphicsBuffer;

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
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<NameComponent> _nameLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();
			state.RequireForUpdate<RoomPartition>();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_nameLookup = state.GetComponentLookup<NameComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_roomElementLookup.Update(ref state);
			_nameLookup.Update(ref state);

			state.Dependency = new PlayerActionJob
			{
				RoomElementBufferLookup = _roomElementLookup,
				NameLookup = _nameLookup,
			}.Schedule(state.Dependency);

			state.Dependency = new PlayerMovementJob().Schedule(state.Dependency);
		}

		[BurstCompile]
		public partial struct PlayerMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PlayerController controller, in ActionController actionController)
			{
				movement.Input = actionController.IsResolving ? float2.zero : controller.MoveInput;
			}
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct PlayerActionJob : IJobEntity
		{
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<NameComponent> NameLookup;

			public void Execute(
				Entity entity,
				ref PlayerController controller,
				ref ActionController actionController,
				ref Orientation orientation,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition,
				in CreditsComponent credits,
				EnabledRefRW<IsActing> isActing)
			{
				// reset
				controller.PrimaryAction.Reset();
				controller.SecondaryAction.Reset();
				controller.ActionTimer = 0f;

				if (actionController.IsResolving)
				{
					// already acting
					controller.ActionTimer = actionController.Action.Time - actionController.Timer;
					return;
				}

				// cannot act if not in partition
				if (partition.CurrentRoom == Entity.Null) return;

				// carry item actions
				if (pick.Picked != Entity.Null)
				{
					controller.SetSecondaryAction(new ActionData(pick.Picked, ActionType.Drop, position.Value + new float2(Const.DropOffsetX * orientation.Value, Const.DropOffsetY), 0f, 0f, 0), in NameLookup, pick.Picked);
				}

				// retrieve relevant action types
				ActionType filter = ActionType.AllActions;

				if (pick.Picked != Entity.Null)
				{
					filter &= ~ActionType.Pick;
				}

				// player scan all close items to list actions
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						RoomElementBufferElement target = enumerator.Current;
						if (target.Entity != entity && Utilities.HasActionType(target.ActionFlags, filter) && position.IsInRange(target.Position, target.Range))
						{
							// primary
							if (target.HasActionType(ActionType.Collect))
							{
								controller.SetPrimaryAction(target.ToActionData(ActionType.Collect), in NameLookup, Entity.Null);
							}
							else if (!controller.HasPrimaryAction && pick.Picked != Entity.Null && target.HasActionType(ActionType.Trash))
							{
								controller.SetPrimaryAction(target.ToActionData(ActionType.Trash), in NameLookup, pick.Picked);
							}

							// secondary
							if (!controller.HasSecondaryAction && target.HasActionType(ActionType.Pick))
							{
								controller.SetSecondaryAction(target.ToActionData(ActionType.Pick), in NameLookup, pick.Picked);
							}
						}
					}
				}

				// start interacting if needed/able
				if (controller.PrimaryAction.IsPressed && controller.HasPrimaryAction &&
					(controller.PrimaryAction.Data.Cost <= 0 || controller.PrimaryAction.Data.Cost <= credits.Value))
				{
					isActing.ValueRW = true;
					actionController.Action = controller.PrimaryAction.Data;
					actionController.Start();
					orientation.Update(controller.PrimaryAction.Data.Position.x - position.x);
				}
				else if (
					controller.SecondaryAction.IsPressed && controller.HasSecondaryAction && 
					(controller.SecondaryAction.Data.Cost <= 0 || controller.SecondaryAction.Data.Cost <= credits.Value))
				{
					isActing.ValueRW = true;
					actionController.Action = controller.SecondaryAction.Data;
					actionController.Start();
					orientation.Update(controller.SecondaryAction.Data.Position.x - position.x);

				}

				// consume inputs
				controller.PrimaryAction.IsPressed = false;
				controller.SecondaryAction.IsPressed = false;
			}
		}
	}
}