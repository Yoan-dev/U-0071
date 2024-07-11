using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	public partial struct MovementSystem : ISystem
	{
		private ComponentLookup<CarryComponent> _pickLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;
		private ComponentLookup<DoorComponent> _doorLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Partition>();

			_pickLookup = SystemAPI.GetComponentLookup<CarryComponent>(true);
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
			_doorLookup = state.GetComponentLookup<DoorComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_pickLookup.Update(ref state);
			_roomElementLookup.Update(ref state);
			_doorLookup.Update(ref state);

			Partition partition = SystemAPI.GetSingleton<Partition>();

			state.Dependency = new MovementJob
			{
				Partition = partition,
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new PushedJob
			{
				Partition = partition,
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new CarryPositionJob().ScheduleParallel(state.Dependency);

			state.Dependency = new PickablePositionJob
			{
				PickLookup = _pickLookup,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new DecollisionJob
			{
				Partition = partition,
				RoomElementBufferLookup = _roomElementLookup,
				DoorLookup = _doorLookup,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		public partial struct MovementJob : IJobEntity
		{
			[ReadOnly]
			public Partition Partition;
			public float DeltaTime;

			public void Execute(ref PositionComponent position, ref Orientation orientation, in MovementComponent movement)
			{
				// input should already be normalized
				float2 input = movement.Input * movement.Speed * DeltaTime;
				float2 newPosition = position.Value + input;
				
				// rough collision
				if (!input.Equals(float2.zero) && Partition.IsPathable(newPosition))
				{
					position.Value = newPosition;
					position.MovedFlag = true;
				}
				orientation.Update(input.x);
			}
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		public partial struct PushedJob : IJobEntity
		{
			[ReadOnly]
			public Partition Partition;
			public float DeltaTime;

			public void Execute(ref PushedComponent pushed, ref PositionComponent position, EnabledRefRW<PushedComponent> pushedRef)
			{
				// input should already be normalized
				// assumed non-zero (push parameters)
				float2 newPosition = position.Value + pushed.Direction * Const.PushedSpeed * DeltaTime;

				// rough collision
				if (Partition.IsPathable(newPosition))
				{
					position.Value = newPosition;
					position.MovedFlag = true;
				}

				pushed.Timer -= DeltaTime;
				if (pushed.Timer <= 0f)
				{
					pushedRef.ValueRW = false;
				}
			}
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		[WithAll(typeof(MovementComponent))]
		public partial struct DecollisionJob : IJobEntity
		{
			[ReadOnly]
			public Partition Partition;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<DoorComponent> DoorLookup;

			public void Execute(Entity entity, ref PositionComponent position, in PartitionComponent partition, in InteractableComponent interactable)
			{
				// rough decollision to avoid characters stacking on each others / on devices
				// will not trigger between rooms

				float2 decollision = float2.zero;
				Entity doorEntity = Entity.Null;
				float2 doorPosition = float2.zero;
				DynamicBuffer<RoomElementBufferElement> elements = RoomElementBufferLookup[partition.CurrentRoom];
				using (var enumerator = elements.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						if (enumerator.Current.HasActionFlag(ActionFlag.Open))
						{
							doorEntity = enumerator.Current.Entity;
							doorPosition = enumerator.Current.Position;
						}
						else if (enumerator.Current.Entity != entity && enumerator.Current.Interactable.CollisionRadius > 0f)
						{
							float2 difference = position.Value - enumerator.Current.Position;
							float radiusSum = interactable.CollisionRadius + enumerator.Current.Interactable.CollisionRadius;

							// circle to circle check
							if (math.lengthsq(difference) <= math.pow(radiusSum, 2f))
							{
								// decollision
								float distance = math.length(difference);
								if (distance > 0f)
								{
									decollision += difference * (radiusSum - distance) / distance * Const.DecollisionStrength;
								}
							}
						}
					}
				}

				float2 newPosition = position.Value + decollision;
				if (Partition.IsPathable(newPosition))
				{
					position.Value = newPosition;
					position.MovedFlag = position.MovedFlag || !decollision.Equals(float2.zero);
				}

				// dirty door collision
				// ideally they would be in the partition ("isPathable") and updated on door open/close
				// but this would do for now
				if (doorEntity != Entity.Null)
				{
					// note: if we detect the door in the partition, it means it is open
					// (no ActionFlag.Open otherwise)

					DoorComponent door = DoorLookup[doorEntity];
					
					// vertical door
					if (door.CodeRequirementFacing.x != 0f &&
						position.x >= doorPosition.x - door.Collision.x &&
						position.x <= doorPosition.x + door.Collision.x)
					{
						position.Value.x = doorPosition.x + (door.Collision.x + math.EPSILON) * math.sign(position.Value.x - doorPosition.x);
						position.MovedFlag = true;
					}
					// horizontal door
					else if (
						door.CodeRequirementFacing.y != 0f &&
						position.y >= doorPosition.y - door.Collision.y &&
						position.y <= doorPosition.y + door.Collision.y)
					{
						position.Value.y = doorPosition.y + (door.Collision.y + math.EPSILON) * math.sign(position.Value.y - doorPosition.y);
						position.MovedFlag = true;
					}
				}
			}
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		[WithAll(typeof(CarryComponent))]
		public partial struct CarryPositionJob : IJobEntity
		{
			public void Execute(ref CarryComponent carry, in PositionComponent position, in Orientation orientation)
			{
				carry.Position = new float2(position.x + Const.CarriedOffsetX * orientation.Value, position.y + Const.CarriedOffsetY);
				carry.YOffset = position.CurrentYOffset;
			}
		}

		[BurstCompile]
		[WithAll(typeof(PickableComponent))]
		public partial struct PickablePositionJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			[ReadOnly]
			public ComponentLookup<CarryComponent> PickLookup;

			public void Execute(ref PositionComponent position, in PickableComponent pickable)
			{
				// we assume Carrier entity is not Null
				CarryComponent carry = PickLookup[pickable.Carrier];
				position.Value = new float2(carry.Position.x, carry.Position.y + pickable.CarriedZOffset);
				position.CurrentYOffset = carry.YOffset + Const.CarriedYOffset;
			}
		}
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MovementSystem))]
	public partial struct TransformSystem : ISystem
	{
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new TransformUpdateJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(DeviceTag))]
		public partial struct TransformUpdateJob : IJobEntity
		{
			public void Execute(ref LocalTransform transform, in PositionComponent position)
			{
				transform.Position = new float3(position.x, position.CurrentYOffset, position.y);
			}
		}
	}
}