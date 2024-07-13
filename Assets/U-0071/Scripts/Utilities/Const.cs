using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace U0071
{
	public static class Const
	{
		public const int ParallelForCount = 256;
		public const float SimulationResetTime = 1f;

		// Cycle
		public const uint CycleCounterSeedIndex = 123456;

		// AI goal
		public const float AITick = 0.1f;
		public const float AILightHungerRatio = 0.85f;
		public const float AIStarvingRatio = 0.15f;
		public const int AIDesiredCreditsToEat = 10;
		public const float AIBaseWorkWeight = 0.5f;
		public const float AIMaxBoredomWeight = 0.7f;
		public const float AIBoredomSpeed = 0.001f;
		public const float AIFulfilmentSpeed = -0.002f; // decrease boredom

		// doors
		public const float AIUnitEnterCodeTime = 4f;
		public const float PeekingStartRange = 5.5f;
		public const float PeekingRange = 2f;
		public const float PeekingAngle = math.PI / 4f;
		public const float PeekingBubbleMinScale = 0.45f;
		public const float PeekingBubbleScaleSmoothStart = 0.7f;
		public const float PeekingBubbleScaleSmoothEnd = 1f;

		// used for Y sorting
		public const float PickableYOffset = 0.4f;
		public const float CharacterYOffset = 1.0f;
		public const float CarriedYOffset = 0.001f;
		public const float YOffsetRatioX = 0.05f;
		public const float YOffsetRatioY = 0.2f;
		public const float CarriedOffsetX = 0.225f;
		public const float CarriedOffsetY = 0.125f;
		public const float CorpseCarriedOffsetZ = 0f;
		public const float DropOffsetX = 0.35f;
		public const float DropOffsetY = 0f;

		// miscellaneous
		public const int LootCreditsCount = 10;
		public const float PushedTimer = 0.35f;
		public const float PushedSpeed = 3.75f;
		public const float EatingHungerGain = 5f;
		public const float MaxHunger = 10f;
		public const float HungerDepleteRate = 0.1f;
		public const float DeathSkinToneOffset = 0.15f;
		public const float DecollisionStrength = 0.6f;
		public const float CharacterZOffset = 0.3f;
		public const float Small = 0.0001f;

		// sickness
		public const float ContaminatedEatingHungerGain = 3f;
		public const float SickSkinToneOffset = 0.1f;
		public const float SickSpeedMultiplier = 0.66f;
		public const float SickHungerDepleteRate = 0.05f;
		public const float SickTime = 30f;

		// UI
		public const float CodepadButtonFeedbackTime = 0.175f;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetActionTime(ActionFlag type, float baseTime)
		{
			// not very fancy but otherwise would need an 
			//  interaction timeper item per action type
			return type switch
			{
				ActionFlag.Pick => 0f,
				ActionFlag.Drop => 0f,
				ActionFlag.Push => 0.25f,
				_ => baseTime,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetReassessmentTimer(AIGoal goal)
		{
			return goal switch
			{
				AIGoal.Eat => 15f,
				AIGoal.Work => 5f,
				AIGoal.Wander => 15f,
				AIGoal.Flee => 5f,
				AIGoal.Destroy => 10f,
				AIGoal.Process => 10f,
				_ => 10f,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float2 GetDropOffset(float orientation)
		{
			return new float2(DropOffsetX * orientation, DropOffsetY);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetStartingCredits(AreaAuthorization authorization)
		{
			return authorization switch
			{
				AreaAuthorization.LevelOne => 20,
				AreaAuthorization.LevelTwo => 30,
				AreaAuthorization.LevelThree => 40,
				AreaAuthorization.Admin => 100,
				_ => 0,
			};
		}
	}
}