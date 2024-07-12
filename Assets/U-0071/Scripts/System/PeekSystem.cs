using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements.Experimental;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(MovementSystem))]
	[UpdateAfter(typeof(HealthSystem))]
	public partial struct PeekSystem : ISystem
	{
		private ComponentLookup<PositionComponent> _positionLookup;
		private ComponentLookup<AuthorizationComponent> _authorizationLookup;
		private ComponentLookup<ActionController> _actionLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PlayerController>();

			_positionLookup = state.GetComponentLookup<PositionComponent>(true);
			_authorizationLookup = state.GetComponentLookup<AuthorizationComponent>(true);
			_actionLookup = state.GetComponentLookup<ActionController>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_positionLookup.Update(ref state);
			_authorizationLookup.Update(ref state);
			_actionLookup.Update(ref state);

			state.Dependency = new PeekingJob
			{
				PlayerEntity = SystemAPI.GetSingletonEntity<PlayerController>(),
				PositionLookup = _positionLookup,
				AuthorizationLookup = _authorizationLookup,
				ActionLookup = _actionLookup,
			}.ScheduleParallel(state.Dependency);
		}
		
		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct PeekingJob : IJobEntity
		{
			public Entity PlayerEntity;
			[ReadOnly] public ComponentLookup<PositionComponent> PositionLookup;
			[ReadOnly] public ComponentLookup<AuthorizationComponent> AuthorizationLookup;
			[ReadOnly] public ComponentLookup<ActionController> ActionLookup;

			[BurstCompile]
			public void Execute(ref PeekingComponent peeking, in DoorComponent door, in PositionComponent position, in InteractableComponent interactable)
			{
				// not relying on partition here because entities in different rooms cannot detect each other (doorway = single tile room)
				if (peeking.StartedFlag || interactable.CurrentUser != Entity.Null && interactable.CurrentUser != PlayerEntity)
				{
					// TBC direction towards check
					float2 playerPosition = PositionLookup[PlayerEntity].Value;
					bool isPeeking =
						position.IsInRange(playerPosition, Const.PeekingRange) &&
						door.IsOnEnterCodeSide(playerPosition, position.Value) &&
						Utilities.IsDirectionTowards(playerPosition, position.Value - playerPosition, position.Value, Const.PeekingAngle);

					if (isPeeking && !peeking.StartedFlag && door.IsOnEnterCodeSide(PositionLookup[interactable.CurrentUser].Value, position.Value))
					{
						// init
						ActionController controller = ActionLookup[interactable.CurrentUser];
						peeking.StartedFlag = true;
						peeking.DigitIndex = (int)(5 * controller.Timer / controller.Action.Time);
						peeking.FirstDiscovered = peeking.SecondDiscovered = peeking.ThirdDiscovered = peeking.FourthDiscovered = false;
						peeking.StaysTimer = 0f;
						peeking.Suspicion = 0f;

						Debug.Log("Peeking started at digit index " + peeking.DigitIndex);
					}
					else if (peeking.StartedFlag && interactable.CurrentUser != Entity.Null)
					{
						// progress
						ActionController controller = ActionLookup[interactable.CurrentUser];
						int newDigitIndex = (int)(5 * controller.Timer / controller.Action.Time);
						if (isPeeking && newDigitIndex != peeking.DigitIndex)
						{
							// reveal typed digit index
							peeking.RevealDigit(peeking.DigitIndex);
						}
						peeking.DigitIndex = newDigitIndex;

						Debug.Log(
							(peeking.FirstDiscovered ? "X" : peeking.DigitIndex > 0 ? "?" : "_") +
							(peeking.SecondDiscovered ? "X" : peeking.DigitIndex > 1 ? "?" : "_") +
							(peeking.ThirdDiscovered ? "X" : peeking.DigitIndex > 2 ? "?" : "_") + "_");
					}
					else if (peeking.StartedFlag) // interaction stopped
					{
						peeking.StartedFlag = false;

						if (isPeeking && door.OpenTimer > 0f && peeking.StaysTimer == 0f)
						{
							// was peeking when door opened, reveal last digit
							peeking.FourthDiscovered = true;
							peeking.DigitIndex = 4;
						}
						// else interaction was cancelled

						Debug.Log(
							(peeking.FirstDiscovered ? "X" : "?") +
							(peeking.SecondDiscovered ? "X" : "?") +
							(peeking.ThirdDiscovered ? "X" : "?") +
							(peeking.FourthDiscovered ? "X" : "?"));
					}
				}
			}
		}
	}
}