using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Searcher;
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
	[UpdateAfter(typeof(RoomSystem))]
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
		[WithNone(typeof(DeathComponent))]
		[WithNone(typeof(PushedComponent))]
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
				in CarryComponent carry,
				in PartitionComponent partition,
				in CreditsComponent credits,
				EnabledRefRO<DeathComponent> death,
				EnabledRefRO<PushedComponent> pushed,
				EnabledRefRW<IsActing> isActing)
			{
				// reset
				controller.PrimaryAction.Reset();
				controller.SecondaryAction.Reset();
				controller.ActionTimer = 0f;

				if (Utilities.ProcessUnitControllerStart(ref actionController, ref orientation, in position, in carry, in partition, isActing, death, pushed))
				{
					if (actionController.IsResolving)
					{
						// update UI
						controller.ActionTimer = actionController.Action.Time - actionController.Timer;
					}
					// consume inputs
					controller.PrimaryAction.IsPressed = false;
					controller.SecondaryAction.IsPressed = false;
					return;
				}

				// retrieve relevant action types
				ActionFlag primaryFilter = ActionFlag.Store | ActionFlag.Destroy | ActionFlag.Collect | ActionFlag.Search | ActionFlag.Push;

				if (carry.Picked != Entity.Null)
				{
					// consider carried item actions
					if (Utilities.HasItemFlag(carry.Flags, ItemFlag.Food))
					{
						controller.SetPrimaryAction(new ActionData(carry.Picked, ActionFlag.Eat, position.Value, 0f, carry.Time, 0), in NameLookup, carry.Picked);
					}

					// set drop action
					controller.SetSecondaryAction(new ActionData(carry.Picked, ActionFlag.Drop, position.Value + Const.GetDropOffset(orientation.Value), 0f, 0f, 0), in NameLookup, carry.Picked);
				}

				// player assess all elements in range
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						RoomElementBufferElement target = enumerator.Current;
						if (target.Entity != entity && position.IsInRange(target.Position, target.Range))
						{
							// primary
							// (override carried item action)
							if (Utilities.HasActionFlag(target.ActionFlags, primaryFilter) &&
								target.Evaluate(controller.PrimaryAction.Type, primaryFilter, carry.Flags, out ActionFlag selectedActionFlag, true))
							{
								controller.SetPrimaryAction(target.ToActionData(selectedActionFlag), in NameLookup, carry.Picked);
							}
							
							// secondary
							// for now, secondary is hard-coded pick/drop
							if (!controller.HasSecondaryAction && target.HasActionFlag(ActionFlag.Pick))
							{
								controller.SetSecondaryAction(target.ToActionData(ActionFlag.Pick), in NameLookup, Entity.Null);
							}
						}
					}
				}

				// start interacting if needed/able
				if (controller.ShouldStartAction(in controller.PrimaryAction, credits.Value))
				{
					isActing.ValueRW = true;
					actionController.Action = controller.PrimaryAction.Data;
					actionController.Start();
					orientation.Update(controller.PrimaryAction.Data.Position.x - position.x);
				}
				else if (controller.ShouldStartAction(in controller.SecondaryAction, credits.Value))
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