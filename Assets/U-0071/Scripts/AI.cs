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
		public void ChooseGoal(bool hasItem, in ComponentLookup<RoomComponent> roomLookup, Entity currentRoom)
		{
			AIGoal newGoal =
				EatWeight > WorkWeight && EatWeight > RelaxWeight ? AIGoal.Eat :
				WorkWeight > RelaxWeight ? AIGoal.Work :
				AIGoal.Relax;

			if (Goal == AIGoal.Destroy && hasItem && !IsCriticalGoal(newGoal))
			{
				// keep going for destroy
				Goal = AIGoal.Destroy; 
			}

			if (Goal == AIGoal.Work)
			{
				// verify that we are not in a crowded workplace
				RoomComponent room = roomLookup[currentRoom];
				if (room.Capacity > 0 && room.Population > room.Capacity)
				{
					// look for new opportunities
					newGoal = AIGoal.Wander;
				}
			}

			Goal = newGoal;
			ReassessmentTimer = Const.GetReassessmentTimer(newGoal);

			// flee goal will be set outside of controller
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsCriticalGoal(AIGoal goal)
		{
			return goal == AIGoal.Eat || goal == AIGoal.Flee;
		}
	}

	public struct AIUnitInitTag : IComponentData, IEnableableComponent { }
}