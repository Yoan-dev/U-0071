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
		private ComponentLookup<PickComponent> _pickLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			_pickLookup = SystemAPI.GetComponentLookup<PickComponent>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_pickLookup.Update(ref state);

			state.Dependency = new MovementJob
			{
				DeltaTime = SystemAPI.Time.DeltaTime,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new PickPositionJob().ScheduleParallel(state.Dependency);

			state.Dependency = new PickablePositionJob
			{
				PickLookup = _pickLookup,
			}.ScheduleParallel(state.Dependency);

		}

		[BurstCompile]
		[WithAll(typeof(PickComponent))]
		public partial struct PickPositionJob : IJobEntity
		{
			public void Execute(ref PickComponent pick, in PositionComponent position, in Orientation orientation)
			{
				pick.Position = new float2(position.x + Const.CarriedOffset.x * orientation.Value, position.y + Const.CarriedOffset.y);
				pick.YOffset = position.CurrentYOffset;
			}
		}

		[BurstCompile]
		[WithAll(typeof(PickableComponent))]
		public partial struct PickablePositionJob : IJobEntity
		{
			[NativeDisableParallelForRestriction]
			[ReadOnly]
			public ComponentLookup<PickComponent> PickLookup;

			public void Execute(ref PositionComponent position, in PickableComponent picked)
			{
				// we assume Carrier entity is not Null
				PickComponent pick = PickLookup[picked.Carrier];
				position.Value = pick.Position;
				position.CurrentYOffset = pick.YOffset + Const.CarriedItemYOffset;
			}
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