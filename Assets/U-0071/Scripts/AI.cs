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
	}

	public struct AIController : IComponentData
	{
		public AIGoal Goal;
		public float ReassessmentTimer;
		public float RoomPathingTimer;
		public float BoredomValue;

		// cached for debug purposes
		public float EatWeight;
		public float WorkWeight;
		public float RelaxWeight;
		public float WanderWeight;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ShouldReassess(float hungerRatio)
		{
			return ReassessmentTimer <= 0f || Goal == AIGoal.Eat && hungerRatio >= Const.AILightHungerRatio;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ChoseGoal()
		{
			Goal =
				EatWeight > WorkWeight && EatWeight > RelaxWeight && EatWeight > WanderWeight ? AIGoal.Eat :
				WorkWeight > RelaxWeight && WorkWeight > WanderWeight ? AIGoal.Work :
				RelaxWeight > WanderWeight ? AIGoal.Relax :
				AIGoal.Wander;
			
			// flee goal will be set outside of controller
		}
	}
}