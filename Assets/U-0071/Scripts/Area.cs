using System;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[Flags]
	public enum AreaAuthorization
	{
		LevelOne = 1 << 0,
		LevelTwo = 1 << 1,
		LevelThree = 1 << 2,
		Red = 1 << 3,
		Blue = 1 << 4,
		Yellow = 1 << 5,
	}

	public struct AuthorisationComponent : IComponentData
	{
		public AreaAuthorization AreaFlag;
	}

	public struct DoorComponent : IComponentData, IEnableableComponent
	{
		public AreaAuthorization AreaFlag;
		public BoundsComponent CachedBounds;
		public float2 CodeRequirementFacing;
		public float OpenTimer;
		public float StaysOpenTime;
		public float AnimationCubicStrength;
		public int StageCount;
	}
}