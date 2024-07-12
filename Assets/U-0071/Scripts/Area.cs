using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;

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

	public struct AuthorizationComponent : IComponentData
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsOnEnterCodeSide(float2 position, float2 doorPosition)
		{
			return CodeRequirementFacing.x != 0f && CodeRequirementFacing.x == 1f && position.x > doorPosition.x ||
				CodeRequirementFacing.x != 0f && CodeRequirementFacing.x == -1f && position.x < doorPosition.x ||
				CodeRequirementFacing.y != 0f && CodeRequirementFacing.y == 1f && position.y > doorPosition.y ||
				CodeRequirementFacing.y != 0f && CodeRequirementFacing.y == -1f && position.y < doorPosition.y;
		}
	}

	public struct PeekingComponent : IComponentData
	{
		public int DigitIndex;
		public float Suspicion;
		public float StaysTimer;
		public bool FirstDiscovered;
		public bool SecondDiscovered;
		public bool ThirdDiscovered;
		public bool FourthDiscovered;
		public bool StartedFlag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RevealDigit(int newDigitIndex)
		{
			if (newDigitIndex == 0) FirstDiscovered = true;
			else if (newDigitIndex == 1) SecondDiscovered = true;
			else if (newDigitIndex == 2) ThirdDiscovered = true;
			else if (newDigitIndex == 3) FourthDiscovered = true;
		}
	}
}