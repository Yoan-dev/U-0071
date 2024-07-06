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

			public void Execute(ref LocalTransform transform, in MovementComponent movement)
			{
				// input should already be normalized
				transform.Position += new float3(movement.Input.x * movement.Speed * DeltaTime,	0f,	movement.Input.y * movement.Speed * DeltaTime);
			}
		}
	}
}