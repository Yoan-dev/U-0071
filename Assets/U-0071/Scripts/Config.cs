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

    public struct UnitIdentityCollection
    {
		public BlobArray<UnitIdentity> Identities;
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
		public Animation CharacterCrushedFromBelow;
		public Animation CharacterFlee;
		public float4 LevelOneShirtColor;
		public float4 LevelTwoShirtColor;
		public float4 LevelThreeShirtColor;
		public float4 AdminShirtColor;
		public int2 WorldDimensions;
		public BlobAssetReference<UnitIdentityCollection> UnitIdentityData;
		public Entity OrganicWastePrefab;
		public float ChanceOfLongHair;
		public float ChanceOfShortHair;
		public float ChanceOfBeard;
		public float CycleDuration;
		public int Iteration;
		public uint Seed;
	}

	public struct GameInitFlag : IComponentData { }
}