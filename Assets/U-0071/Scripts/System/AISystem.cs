using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Searcher;
using static UnityEditor.Rendering.FilterWindow;
using static UnityEngine.GraphicsBuffer;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(RoomSystem))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<InteractableComponent> _interactableLookup;
		private ComponentLookup<PickableComponent> _pickableLookup;
		private EntityQuery _query;
		private float _timer;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<RoomPartition>();

			_query = SystemAPI.QueryBuilder()
				.WithAllRW<ActionController, Orientation>()
				.WithAll<AITag, PositionComponent, PartitionComponent, CreditsComponent>()
				.WithPresent<CarryComponent, DeathComponent, PushedComponent>()
				.WithPresentRW<IsActing>()
				.Build();

			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_interactableLookup = state.GetComponentLookup<InteractableComponent>(true);
			_pickableLookup = state.GetComponentLookup<PickableComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			// TODO: batching
			_timer += SystemAPI.Time.DeltaTime;
			if (_timer >= Const.AITick)
			{
				_timer -= Const.AITick;

				_roomElementLookup.Update(ref state);
				_interactableLookup.Update(ref state);
				_pickableLookup.Update(ref state);

				state.Dependency = new AIActionJob
				{
					RoomElementBufferLookup = _roomElementLookup,
					InteractableLookup = _interactableLookup,
					PickableLookup = _pickableLookup,
				}.ScheduleParallel(_query, state.Dependency);

				state.Dependency = new AIMovementJob().ScheduleParallel(state.Dependency);
			}
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct AIActionJob : IJobEntity
		{
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<PickableComponent> PickableLookup;
			[ReadOnly]
			public ComponentLookup<InteractableComponent> InteractableLookup;

			public void Execute(
				Entity entity,
				ref ActionController controller,
				ref Orientation orientation,
				in PositionComponent position,
				in CarryComponent carry,
				in PartitionComponent partition,
				in CreditsComponent credits,
				EnabledRefRW<IsActing> isActing,
				EnabledRefRO<DeathComponent> death,
				EnabledRefRO<PushedComponent> pushed)
			{
				if (Utilities.ProcessUnitControllerStart(ref controller, ref orientation, in position, in carry, in partition, isActing, death, pushed))
				{
					return;
				}

				// re-evaluate current target
				if (controller.HasTarget)
				{
					if (!InteractableLookup.TryGetComponent(controller.Action.Target, out InteractableComponent interactable) ||
						interactable.HasActionFlag(ActionFlag.Pick) && PickableLookup.IsComponentEnabled(controller.Action.Target) ||
						!interactable.HasActionFlag(controller.Action.ActionFlag))
					{
						// target has been destroyed/picked/disabled
						controller.Stop();
					}
					else if (position.IsInRange(controller.Action.Position, controller.Action.Range))
					{
						// start interacting
						isActing.ValueRW = true;
						controller.Start();
						orientation.Update(controller.Action.Position.x - position.Value.x);
						return;
					}

					// going to target
					if (controller.HasTarget)
					{
						return;
					}
				}

				// look for new target

				// retrieve relevant action types
				ActionFlag filter = ActionFlag.Pick | ActionFlag.Store | ActionFlag.Destroy | ActionFlag.Collect | ActionFlag.Search;

				if (carry.Picked != Entity.Null)
				{
					filter &= ~ActionFlag.Pick;

					// consider picked interactable action
					if (Utilities.HasItemFlag(carry.Flags, ItemFlag.Food))
					{
						// start interacting
						isActing.ValueRW = true;
						controller.Action = new ActionData(carry.Picked, ActionFlag.Eat, position.Value, 0f, carry.Time, 0);
						controller.Start();
						orientation.Update(controller.Action.Position.x - position.Value.x);
						return;
					}
				}

				// look for target
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				float minMagn = float.MaxValue;
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						RoomElementBufferElement target = enumerator.Current;
						if (target.Entity != entity &&
							target.HasActionFlag(filter) &&
							(target.Cost <= 0f || target.Cost <= credits.Value) &&
							target.Evaluate(controller.Action.ActionFlag, filter, carry.Flags, out ActionFlag selectedActionFlag))
						{
							float magn = math.lengthsq(position.Value - target.Position);

							// retrieve closest of prioritary type
							if (selectedActionFlag > controller.Action.ActionFlag || magn < minMagn)
							{
								minMagn = magn;
								controller.Action = target.ToActionData(selectedActionFlag);
							}
							// lower prio would have been filtered in target.Evaluate
						}
					}
				}

				if (controller.HasTarget && position.IsInRange(controller.Action.Position, controller.Action.Range))
				{
					// immediately interact
					isActing.ValueRW = true;
					controller.Start();
					orientation.Update(controller.Action.Position.x - position.Value.x);
				}
				// else start moving to target
			}
		}

		[BurstCompile]
		[WithAll(typeof(AITag))]
		[WithNone(typeof(DeathComponent))]
		[WithNone(typeof(PushedComponent))]
		public partial struct AIMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PositionComponent position, in ActionController controller)
			{
				if (!controller.IsResolving && controller.HasTarget)
				{
					float2 direction = controller.Action.Position - position.Value;
					movement.Input = math.lengthsq(direction) > math.pow(controller.Action.Range, 2f) ? math.normalize(direction) : float2.zero;
				}
				else
				{
					movement.Input = float2.zero;
				}
			}
		}

	}
}