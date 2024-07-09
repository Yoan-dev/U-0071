using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace U0071
{
	public static class Const
	{
		public const float AITick = 0.1f;
		public const float DeviceYOffset = 0.1f;
		public const float PickableYOffset = 0.4f;
		public const float CharacterYOffset = 0.7f;
		public const float CarriedYOffset = 0.001f;

		// used for Y sorting
		public const float YOffsetRatioX = 0.05f;
		public const float YOffsetRatioY = 0.2f;

		public const float CarriedOffsetX = 0.225f;
		public const float CarriedOffsetY = 0.125f;
		public const float CorpseCarriedOffsetZ = 0f;
		public const float DropOffsetX = 0.35f;
		public const float DropOffsetY = 0f;

		public const int LootCreditsCount = 10;
		public const float PushedTimer = 0.35f;
		public const float PushedSpeed = 3.75f;
		public const float EatingHungerGain = 5f;
		public const float MaxHunger = 10f;
		public const float HungerDepleteRate = 0.1f;
		public const float DeathSkinToneOffset = 0.15f;

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
		public static float2 GetDropOffset(float orientation)
		{
			return new float2(Const.DropOffsetX * orientation, Const.DropOffsetY);
		}
	}
}