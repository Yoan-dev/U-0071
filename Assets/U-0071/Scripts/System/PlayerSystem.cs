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
	public partial struct PlayerControllerSystem : ISystem, ISystemStartStop
	{
		private NativeQueue<ActionEvent>.ParallelWriter _actionEventWriter; // from ActionSystem
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

		public void OnStartRunning(ref SystemState state)
		{
			_actionEventWriter = Utilities.GetSystem<ActionSystem>(ref state).EventQueueWriter;
		}

		public void OnStopRunning(ref SystemState state)
		{
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_roomElementLookup.Update(ref state);
			_nameLookup.Update(ref state);

			state.Dependency = new PlayerMovementJob().Schedule(state.Dependency);

			state.Dependency = new PlayerActionJob
			{
				ActionEventWriter = _actionEventWriter,
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
			[WriteOnly]
			public NativeQueue<ActionEvent>.ParallelWriter ActionEventWriter;
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
					controller.SetSecondaryAction(new ActionData(pick.Picked, ActionType.Drop, position.Value + new float2(Const.DropOffsetX * orientation.Value, Const.DropOffsetY), 0f, 0), in NameLookup, pick.Picked);
				}

				// retrieve relevant action types
				ActionType filter = ActionType.All;

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
							if (target.HasActionType(ActionType.Buy))
							{
								controller.SetPrimaryAction(target.ToActionData(ActionType.Buy), in NameLookup, Entity.Null);
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

				// queue action if needed/able
				if (controller.PrimaryAction.IsPressed && controller.HasPrimaryAction &&
					(controller.PrimaryAction.Data.Cost <= 0 || controller.PrimaryAction.Data.Cost <= credits.Value))
				{
					ActionEventWriter.Enqueue(new ActionEvent(entity, in controller.PrimaryAction.Data));
					orientation.Update(controller.PrimaryAction.Data.Position.x - position.x);
				}
				else if (
					controller.SecondaryAction.IsPressed && controller.HasSecondaryAction && 
					(controller.SecondaryAction.Data.Cost <= 0 || controller.SecondaryAction.Data.Cost <= credits.Value))
				{
					ActionEventWriter.Enqueue(new ActionEvent(entity, in controller.SecondaryAction.Data));
					orientation.Update(controller.SecondaryAction.Data.Position.x - position.x);

				}

				// consume inputs
				controller.PrimaryAction.IsPressed = false;
				controller.SecondaryAction.IsPressed = false;
			}
		}
	}
}