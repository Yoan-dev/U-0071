using System.Runtime.CompilerServices;

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
		public const float CarriedOffsetY = -0.175f;
		public const float DropOffsetX = 0.35f;
		public const float DropOffsetY = -0.3f;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float GetActionTime(ActionType type, float baseTime)
		{
			// not very fancy but otherwise would need an 
			//  interaction timeper item per action type
			return type switch
			{
				ActionType.Pick => 0f,
				ActionType.Drop => 0f,
				_ => baseTime,
			};
		}
	}
}