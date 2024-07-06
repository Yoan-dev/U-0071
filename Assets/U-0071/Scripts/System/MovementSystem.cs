using Unity.Burst;
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
		public partial struct MovementJob : IJobEntity
		{
			public float DeltaTime;

			public void Execute(ref PositionComponent position, in MovementComponent movement)
			{
				// input should already be normalized
				position.Value += movement.Input * movement.Speed * DeltaTime;

				// TODO: enable MovedFlag if needed
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
		// TODO: filter by MovedFlag
		public partial struct TransformUpdateJob : IJobEntity
		{
			public void Execute(ref LocalTransform transform, in PositionComponent position)
			{
				transform.Position = new float3(position.x, transform.Position.y, position.y);
			}
		}
	}
}