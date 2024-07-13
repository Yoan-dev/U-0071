using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(TransformSystem))]
	public partial struct EndingSystem : ISystem
	{
		private float _phaseFourTimer;

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

				// lock last door
				ref InteractableComponent interactable = ref SystemAPI.GetComponentRW<InteractableComponent>(ending.LastDoorEntity).ValueRW;
				interactable.ActionFlags = 0;
				interactable.Changed = true;

			}
			else if (!ending.PhaseTwoTriggered && position.y > ending.EndingPhaseTwoY)
			{
				ending.PhaseTwoTriggered = true;

			}
			else if (!ending.PhaseThreeTriggered && position.y > ending.EndingPhaseThreeY && math.abs(position.x) > ending.EndingPhaseThreeAbsX)
			{
				ending.PhaseThreeTriggered = true;
				
				SystemAPI.GetComponentRW<AnimationController>(playerEntity).ValueRW.StartAnimation(ending.CharacterDepixelate);
				SystemAPI.GetComponentRW<PlayerController>(playerEntity).ValueRW.Locked = true;

			}
			else if (!ending.PhaseFourTriggered && ending.PhaseThreeTriggered)
			{
				_phaseFourTimer += SystemAPI.Time.DeltaTime;
				if (_phaseFourTimer > Const.EndingPhaseFourTime)
				{
					SystemAPI.GetComponentRW<PlayerController>(playerEntity).ValueRW.Locked = false;
					SystemAPI.GetComponentRW<MovementComponent>(playerEntity).ValueRW.FreeMovement = true;
					state.EntityManager.RemoveComponent<PartitionComponent>(playerEntity); // TBD if safe
				}
			}
		}
	}
}