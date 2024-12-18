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
		}
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(RoomSystem))]
	[UpdateBefore(typeof(ActionSystem))]
	public partial struct PlayerControllerSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();
			state.RequireForUpdate<Partition>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new PlayerActionJob
			{
				RoomElementBufferLookup = SystemAPI.GetBufferLookup<RoomElementBufferElement>(true),
				InteractableLookup = SystemAPI.GetComponentLookup<InteractableComponent>(true),
				PickableLookup = SystemAPI.GetComponentLookup<PickableComponent>(true),
				NameLookup = SystemAPI.GetComponentLookup<NameComponent>(true),
				ActionNameLookup = SystemAPI.GetComponentLookup<ActionNameComponent>(true),
				DoorLookup = SystemAPI.GetComponentLookup<DoorComponent>(true),
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
				movement.Input = actionController.IsResolving || controller.Locked ? float2.zero : controller.MoveInput;
			}
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct PlayerActionJob : IJobEntity
		{
			[ReadOnly] public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly] public ComponentLookup<NameComponent> NameLookup;
			[ReadOnly] public ComponentLookup<ActionNameComponent> ActionNameLookup;
			[ReadOnly] public ComponentLookup<PickableComponent> PickableLookup;
			[ReadOnly] public ComponentLookup<InteractableComponent> InteractableLookup;
			[ReadOnly] public ComponentLookup<DoorComponent> DoorLookup;

			public void Execute(
				Entity entity,
				ref PlayerController controller,
				ref ActionController actionController,
				ref Orientation orientation,
				in PositionComponent position,
				in CarryComponent carry,
				in PartitionInfoComponent partitionInfo,
				in CreditsComponent credits,
				EnabledRefRO<DeathComponent> death,
				EnabledRefRO<PushedComponent> pushed,
				EnabledRefRW<IsActing> isActing)
			{
				// reset
				controller.PrimaryAction.Reset();
				controller.SecondaryAction.Reset();
				controller.ActionTimer = 0f;

				if (Utilities.ProcessUnitControllerStart(entity, ref actionController, in orientation, in position, in partitionInfo, isActing, death, pushed, in InteractableLookup, in PickableLookup, in carry))
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
				ActionFlag primaryFilter = ActionFlag.Store | ActionFlag.Destroy | ActionFlag.Collect | ActionFlag.Search | ActionFlag.Push | ActionFlag.Contaminate;

				if (carry.HasItem)
				{
					// consider carried item actions
					if (Utilities.HasItemFlag(carry.Flags, ItemFlag.Food))
					{
						controller.SetPrimaryAction(new ActionData(carry.Picked, ActionFlag.Eat, 0, carry.Flags, position.Value, 0f, carry.Time, 0), in NameLookup, in ActionNameLookup, carry.Picked);
					}

					// set drop action
					controller.SetSecondaryAction(new ActionData(carry.Picked, ActionFlag.Drop, 0, carry.Flags, Utilities.GetDropPosition(position.Value, orientation.Value, partitionInfo.ClosestEdgeX), 0f, 0f, 0), in NameLookup, in ActionNameLookup, carry.Picked);
				}

				// player assess all elements in range
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partitionInfo.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						RoomElementBufferElement target = enumerator.Current;
						if (target.CanBeUsed && target.Entity != entity && position.IsInRange(target.HasActionFlag(ActionFlag.Push) ? target.Position : target.Position,  target.Range))
						{
							// door opening is always ok
							if (target.HasActionFlag(ActionFlag.Open))
							{
								DoorComponent door = DoorLookup[target.Entity];
								controller.SetPrimaryAction(target.ToActionData(ActionFlag.Open, target.ItemFlags, carry.Flags), in NameLookup, in ActionNameLookup, carry.Picked);
								if (door.IsOnEnterCodeSide(position.Value, target.Position))
								{
									// stop resolving manually (enter code UI)
									controller.PrimaryAction.Data.Time = int.MaxValue;
									controller.CachedDoorAuthorization = door.AreaFlag;
								}
								else
								{
									controller.CachedDoorAuthorization = 0;
								}
								break;
							}
							// primary
							// (override carried item action)
							else if (
								Utilities.HasActionFlag(target.ActionFlags, primaryFilter) &&
								target.Evaluate(controller.PrimaryAction.Type, primaryFilter, carry.Flags, out ActionFlag selectedActionFlag, carry.HasItem, false, true, carry.HasItem))
							{
								// pose as a storage
								if (selectedActionFlag == ActionFlag.Store && target.Interactable.HasActionFlag(ActionFlag.TeleportItem))
								{
									selectedActionFlag = ActionFlag.TeleportItem;
								}
								controller.SetPrimaryAction(target.ToActionData(selectedActionFlag, target.ItemFlags, carry.Flags), in NameLookup, in ActionNameLookup, carry.Picked);
							}
							// secondary
							// for now, secondary is hard-coded pick/drop
							else if (!controller.HasSecondaryAction && target.HasActionFlag(ActionFlag.Pick))
							{
								controller.SetSecondaryAction(target.ToActionData(ActionFlag.Pick, target.ItemFlags, carry.Flags), in NameLookup, in ActionNameLookup, Entity.Null);
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