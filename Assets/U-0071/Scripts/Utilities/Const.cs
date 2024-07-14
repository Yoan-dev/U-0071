using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.VisualScripting;

namespace U0071
{
	public static class Const
	{
		public const int ParallelForCount = 256;

		// Ending
		public const float EndingPhaseFourTime = 2f;
		public const float EndingPhaseFiveTime = 3f;
		public const float EndCameraSpeed = 6f;

		// Simulation Reset
		public const float SimulationFirstResetTime = 8f;
		public const float SimulationRespawnResetTime = 5f;
		public const float UnitRandomEffectsStartTime = 2f;
		public const float UnitRandomEffectsTime = 0.75f;

		// Cycle
		public const uint CycleCounterSeedIndex = 123456;

		// AI goal
		public const float AILightHungerRatio = 0.85f;
		public const float AIStarvingRatio = 0.15f;
		public const int AIDesiredCreditsToEat = 10;
		public const float AIBaseWorkWeight = 0.5f;
		public const float AIMaxBoredomWeight = 0.7f;
		public const float AIBoredomSpeed = 0.01f;
		public const float AIFulfilmentSpeed = -0.02f; // decrease boredom
		public const float AICantReachTime = 10f;

		// doors
		public const float AICodeTypingRevealValue = 4.5f;
		public const float AIUnitEnterCodeTime = 4f;
		public const float PeekingStartRange = 5.5f;
		public const float PeekingRange = 2f;
		public const float PeekingAngle = math.PI / 4f;
		public const float PeekingBubbleMinScale = 0.45f;
		public const float PeekingBubbleScaleSmoothStart = 0.7f;
		public const float PeekingBubbleScaleSmoothEnd = 1f;
		public const float PeekingSuspicionSpeed = 0.4f;
		public const float PeekingBustedFeedbackTreshold = 0.9f;
		public const float PeekingSuspicionDecreaseRate = 0.01f;
		public const float SicknessAwarenessModifier = -0.3f;
		public const float PanicAwarenessModifier = -0.3f;

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
		public const float EatingHungerGain = 4f;
		public const float MaxHunger = 10f;
		public const float HungerDepleteRate = 0.1f;
		public const float DeathSkinToneOffset = 0.15f;
		public const float DecollisionStrength = 0.6f;
		public const float CharacterZOffset = 0.3f;
		public const float Small = 0.0001f;

		// sickness
		public const float ContaminatedEatingHungerGain = 2f;
		public const float SickSkinToneOffset = 0.15f;
		public const float SickSpeedMultiplier = 0.66f;
		public const float SickHungerDepleteRate = 0.05f;
		public const float SpreadSicknessTime = 9f;
		public const float SpreadSicknessResolveTime = 1.35f;
		public const float SickTime = 30f;
		public const float CorpseContaminationStrength = 0.4f;
		public const float ContaminationRange = 2f;
		public const float ContaminationLevelDepleteRate = 0.05f;
		public const float MaxContaminationLevel = 10f;
		public const float ContaminationSicknessTreshold = 1f;
		public const float ContaminationSpreadDecreaseLevelValue = 0.4f;

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
				AIGoal.BoredWander => 20f,
				AIGoal.WorkWander => 30f,
				AIGoal.Flee => 5f,
				AIGoal.Destroy => 10f,
				AIGoal.Process => 10f,
				_ => 10f,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetCurrentAwareness(bool isSick, AIGoal currentGoal)
		{
			return 1f + (isSick ? SicknessAwarenessModifier : 0f) + (currentGoal == AIGoal.Flee ? PanicAwarenessModifier : 0f);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetSuspicionMultiplier(AreaAuthorization peekerAuthorization, AreaAuthorization authorization)
		{
			return peekerAuthorization == authorization ? 0f :
				authorization == AreaAuthorization.LevelTwo ? 1f :
				authorization == AreaAuthorization.LevelThree ? 1.15f :
				authorization == AreaAuthorization.Admin ? 1.3f : 0f;
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