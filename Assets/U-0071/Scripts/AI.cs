using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public enum AIGoal
	{
		None = 0,
		Eat, // find a place to buy food
		Work, // make money
		BoredWander, // take a break
		WorkWander, // search new opportunities
		Flee, // someone died
		Destroy, // find a place to destroy carried item
		Process, // find a place to process carried raw food
	}

	public struct AIController : IComponentData
	{
		public float2 LastMovementInput;
		public AIGoal Goal;
		public float ReassessmentTimer;
		public float BoredomValue;

		// cached for debug purposes
		public float EatWeight;
		public float WorkWeight;

		public bool IsPathing;

		// for debugging
		public bool ReassessedLastFrame;

		public bool HasCriticalGoal => IsCriticalGoal(Goal);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ShouldReassess(float hungerRatio, bool hasItem, bool hasOpportunity, bool isFired)
		{
			return 
				ReassessmentTimer <= 0f || 
				Goal == AIGoal.Eat && hungerRatio >= Const.AILightHungerRatio ||
				Goal == AIGoal.Destroy && !hasItem ||
				Goal == AIGoal.Process && !hasItem ||
				Goal == AIGoal.WorkWander && hasOpportunity ||
				Goal == AIGoal.Work && isFired;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ChooseGoal(Entity entity, bool hasItem, bool isFired, in ComponentLookup<RoomComponent> roomLookup, Entity currentRoom)
		{
			AIGoal newGoal =
				EatWeight >= WorkWeight && EatWeight >= BoredomValue ? AIGoal.Eat :
				BoredomValue >= WorkWeight ? AIGoal.BoredWander :
				AIGoal.Work;

			if ((Goal == AIGoal.Destroy || Goal == AIGoal.Process) && hasItem && !IsCriticalGoal(newGoal))
			{
				// keep going for destroy/process
				newGoal = Goal; 
			}

			if (Goal == AIGoal.Work && isFired)
			{
				newGoal = AIGoal.WorkWander;
			}

			Goal = newGoal;
			ReassessmentTimer = Const.GetReassessmentTimer(newGoal);

			// flee/destroy/process goals set outside of controller
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsCriticalGoal(AIGoal goal)
		{
			return goal == AIGoal.Eat || goal == AIGoal.Flee;
		}
	}

	public struct AIUnitInitTag : IComponentData, IEnableableComponent { }
}