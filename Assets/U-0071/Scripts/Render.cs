using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace U0071
{
	[MaterialProperty("_Index")]
	public struct TextureArrayIndex : IComponentData
	{
		public float Value;
	}

	[MaterialProperty("_Orientation")]
	public struct Orientation : IComponentData
	{
		public float Value;

		public void Update(float delta)
		{
			if (delta != 0f) Value = math.sign(delta);
		}
	}

	[MaterialProperty("_ShirtColor")]
	public struct ShirtColor : IComponentData
	{
		public float4 Value;
	}

	[MaterialProperty("_SkinColor")]
	public struct SkinColor : IComponentData
	{
		public float4 Value;
	}

	[MaterialProperty("_ShortHairColor")]
	public struct ShortHairColor : IComponentData
	{
		public float4 Value;
	}

	[MaterialProperty("_LongHairColor")]
	public struct LongHairColor : IComponentData
	{
		public float4 Value;
	}

	[MaterialProperty("_BeardColor")]
	public struct BeardColor : IComponentData
	{
		public float4 Value;
	}
}