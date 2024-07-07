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
		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			state.Dependency = new MovementJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
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
	}

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MovementSystem))]
	public partial struct TransformSystem : ISystem
	{
		private ComponentLookup<PositionComponent> _positionLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			_positionLookup = SystemAPI.GetComponentLookup<PositionComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_positionLookup.Update(ref state);

			state.Dependency = new PickedPositionJob
			{
				PositionLookup = _positionLookup,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new TransformUpdateJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		[WithNone(typeof(PickableComponent))]
		public partial struct TransformUpdateJob : IJobEntity
		{
			public void Execute(ref LocalTransform transform, in PositionComponent position)
			{
				transform.Position = new float3(position.x, transform.Position.y, position.y);
			}
		}

		[BurstCompile]
		public partial struct PickedPositionJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			[ReadOnly]
			public ComponentLookup<PositionComponent> PositionLookup;

			public void Execute(ref LocalTransform transform, in PickableComponent picked)
			{
				// we assume Carrier entity is not Null
				float2 position = PositionLookup[picked.Carrier].Value;
				transform.Position = new float3(position.x, transform.Position.y, position.y);
			}
		}
	}
}