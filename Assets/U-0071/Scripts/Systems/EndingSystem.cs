using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(TransformSystem))]
	public partial struct EndingSystem : ISystem
	{
		private float _endingTimer;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Ending>();
			state.RequireForUpdate<PlayerController>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			ref Ending ending = ref SystemAPI.GetSingletonRW<Ending>().ValueRW;

			if (!ending.HaveEnding)
			{
				return;
			}

			Entity playerEntity = SystemAPI.GetSingletonEntity<PlayerController>();
			float2 position = SystemAPI.GetComponent<PositionComponent>(playerEntity).Value;

			if (!ending.PhaseOneTriggered && position.y > ending.EndingPhaseOneY)
			{
				ending.PhaseOneTriggered = true;

				// disable hunger and acting
				state.CompleteDependency();
				state.EntityManager.AddComponent<InvincibleTag>(playerEntity);
				state.EntityManager.RemoveComponent<IsActing>(playerEntity); // TBD if safe

				// reset last controller info
				ref PlayerController controller = ref SystemAPI.GetComponentRW<PlayerController>(playerEntity).ValueRW;
				controller.PrimaryAction.Reset();
				controller.SecondaryAction.Reset();
				controller.ActionTimer = 0f;
			}
			else if (!ending.PhaseTwoTriggered && position.y > ending.EndingPhaseTwoY)
			{
				ending.PhaseTwoTriggered = true;

			}
			else if (!ending.PhaseThreeTriggered &&
				(position.y > ending.EndingPhaseThreeY ||
				position.y > ending.EndingPhaseTwoY && math.abs(position.x) > ending.EndingPhaseThreeAbsX))
			{
				ending.PhaseThreeTriggered = true;
				SystemAPI.GetComponentRW<PlayerController>(playerEntity).ValueRW.Locked = true;
			}
			else if (!ending.PhaseFourTriggered && ending.PhaseThreeTriggered)
			{
				_endingTimer += SystemAPI.Time.DeltaTime;
				if (_endingTimer > Const.EndingPhaseFourTime)
				{
					ending.PhaseFourTriggered = true;
					SystemAPI.GetComponentRW<AnimationController>(playerEntity).ValueRW.StartAnimation(ending.CharacterDepixelate);
					state.EntityManager.AddComponent<SimpleAnimationTag>(playerEntity);
				}
			}
			else if (ending.PhaseFourTriggered && !ending.PhaseFiveTriggered)
			{
				_endingTimer += SystemAPI.Time.DeltaTime;
				if (_endingTimer > Const.EndingPhaseFiveTime)
				{
					ending.PhaseFiveTriggered = true;
					SystemAPI.GetComponentRW<PlayerController>(playerEntity).ValueRW.Locked = false;
					ref MovementComponent movement = ref SystemAPI.GetComponentRW<MovementComponent>(playerEntity).ValueRW;
					movement.FreeMovement = true;
					movement.Speed = Const.EndCameraSpeed;

					state.EntityManager.RemoveComponent<PartitionInfoComponent>(playerEntity); // TBD if safe
				}

			}
		}
	}
}