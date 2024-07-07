using Unity.Mathematics;

namespace U0071
{
	public static class Const
	{
		public const float AITick = 0.1f;
		public const float DeviceYOffset = 0.1f;
		public const float ItemYOffset = 0.4f;
		public const float CharacterYOffset = 0.7f;
		public const float CarriedItemYOffset = 0.001f;
		public static float2 YOffsetRatio = new float2(0.05f, 0.2f); // used for Y sorting
		public static float2 CarriedOffset = new float2(0.225f, -0.175f);
		public static float2 DropItemOffset = new float2(0.35f, -0.3f);
	}
}