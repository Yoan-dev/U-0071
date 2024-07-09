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

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			_pickLookup = SystemAPI.GetComponentLookup<CarryComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_pickLookup.Update(ref state);

			state.Dependency = new MovementJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new PushedJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new CarryPositionJob().ScheduleParallel(state.Dependency);

			state.Dependency = new PickablePositionJob
			{
				PickLookup = _pickLookup,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		[WithNone(typeof(PickableComponent))]
		public partial struct MovementJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref PositionComponent position, ref Orientation orientation, in MovementComponent movement)
			{
				float2 input = movement.Input * movement.Speed * DeltaTime;

				// input should already be normalized
				position.Add(input);
				orientation.Update(input.x);
			}
		}

		[BurstCompile]
		[WithNone(typeof(DeathComponent))]
		public partial struct PushedJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref PushedComponent pushed, ref PositionComponent position, EnabledRefRW<PushedComponent> pushedRef)
			{
				position.Value += pushed.Direction * Const.PushedSpeed * DeltaTime;
				position.MovedFlag = true;

				pushed.Timer -= DeltaTime;
				if (pushed.Timer <= 0f)
				{
					pushedRef.ValueRW = false;
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
		public partial struct TransformUpdateJob : IJobEntity
		{
			public void Execute(ref LocalTransform transform, in PositionComponent position)
			{
				transform.Position = new float3(position.x, position.CurrentYOffset, position.y);
			}
		}
	}
}