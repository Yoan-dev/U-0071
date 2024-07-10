using System.Runtime.CompilerServices;
using Unity.Entities;

namespace U0071
{
	public enum AIGoal
	{
		Eat = 0,
		Work,
		Relax, // chill
		Wander, // find new opportunities
		Flee,
		Destroy, // find a place to destroy carried item
	}

	public struct AIController : IComponentData
	{
		public AIGoal Goal;
		public float ReassessmentTimer;
		public float BoredomValue;

		// cached for debug purposes
		public float EatWeight;
		public float WorkWeight;
		public float RelaxWeight;
		public float WanderWeight;

		// pathing
		public bool IsPathing;

		public bool HasCriticalGoal => IsCriticalGoal(Goal);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ShouldReassess(float hungerRatio, bool hasItem)
		{
			return 
				ReassessmentTimer <= 0f || 
				Goal == AIGoal.Eat && hungerRatio >= Const.AILightHungerRatio ||
				Goal == AIGoal.Destroy && !hasItem;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ChooseGoal(bool hasItem)
		{
			AIGoal newGoal =
				EatWeight > WorkWeight && EatWeight > RelaxWeight && EatWeight > WanderWeight ? AIGoal.Eat :
				WorkWeight > RelaxWeight && WorkWeight > WanderWeight ? AIGoal.Work :
				RelaxWeight > WanderWeight ? AIGoal.Relax :
				AIGoal.Wander;

			if (Goal == AIGoal.Destroy && hasItem && !IsCriticalGoal(newGoal)) return; // keep going for destroy

			Goal = newGoal;

			// flee goal will be set outside of controller
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsCriticalGoal(AIGoal goal)
		{
			return goal == AIGoal.Eat || goal == AIGoal.Flee;
		}
	}
}