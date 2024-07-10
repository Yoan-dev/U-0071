using System;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct UnitRenderingColors
	{
		public BlobArray<float4> SkinColors;
		public BlobArray<float4> HairColors;
	}

	[Serializable]
	public struct Config : IComponentData
	{
		public Animation CharacterIdle;
		public Animation CharacterWalk;
		public Animation CharacterInteract;
		public Animation CharacterDie;
		public Animation CharacterCrushed;
		public int2 WorldDimensions;
		public BlobAssetReference<UnitRenderingColors> UnitRenderingColors;
		public float ChanceOfLongHair;
		public float ChanceOfShortHair;
		public float ChanceOfBeard;
		public uint Seed;
	}
}