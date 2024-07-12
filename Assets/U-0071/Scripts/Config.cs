using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct UnitIdentity : IComponentData
	{
		public FixedString32Bytes Name;
		public int SkinColorIndex;
		public int HairColorIndex;
		public bool HasShortHair;
		public bool HasLongHair;
		public bool HasBeard;
	}

	public struct UnitRenderingColors
	{
		public BlobArray<float4> SkinColors;
		public BlobArray<float4> HairColors;
	}

    public struct UnitIdentityCollection
    {
		public BlobArray<UnitIdentity> Identities;
    }

    [Serializable]
	public struct Config : IComponentData
	{
		public Animation CharacterIdle;
		public Animation CharacterWalk;
		public Animation CharacterInteract;
		public Animation CharacterDie;
		public Animation CharacterCrushed;
		public Animation CharacterCrushedFromBelow;
		public Animation CharacterFlee;
		public float4 LevelOneShirtColor;
		public float4 LevelTwoShirtColor;
		public float4 LevelThreeShirtColor;
		public int2 WorldDimensions;
		public BlobAssetReference<UnitIdentityCollection> UnitNames;
		public BlobAssetReference<UnitRenderingColors> UnitRenderingColors;
		public Entity OrganicWastePrefab;
		public float ChanceOfLongHair;
		public float ChanceOfShortHair;
		public float ChanceOfBeard;
		public float CycleDuration;
		public uint Seed;
	}
}