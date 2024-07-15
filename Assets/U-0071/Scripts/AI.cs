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
		public float CantReachTimer;
		public float BoredomValue;
		public float SuspicionValue;
		public float Awareness;

		// cached for debug purposes
		public float EatWeight;
		public float WorkWeight;

		public bool IsPathing;
		public bool OpportunityFlag;
		public bool EatFlag;

		// for debugging
		public bool ReassessedLastFrame;

		public bool HasCriticalGoal => IsCriticalGoal(Goal);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ShouldReassess(bool hasItem, bool isFired)
		{
			bool eatFlag = EatFlag;
			EatFlag = false; // consume
			return 
				ReassessmentTimer <= 0f || 
				Goal == AIGoal.Eat && eatFlag ||
				Goal == AIGoal.Destroy && !hasItem ||
				Goal == AIGoal.Process && !hasItem ||
				Goal == AIGoal.WorkWander && OpportunityFlag ||
				Goal == AIGoal.Work && isFired;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ChooseGoal(bool hasItem, bool isFired)
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
			if (Goal == AIGoal.WorkWander && newGoal == AIGoal.Work && !OpportunityFlag)
			{
				// continue looking for work
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