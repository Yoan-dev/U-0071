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
		public int CurrentCode;
	}

	public struct DoorComponent : IComponentData
	{
		public AreaAuthorization AreaFlag;
		public float2 Collision;
		public float2 CodeRequirementDirection;
		public float OpenTimer;
		public int StageCount;

		public bool IsOpen => OpenTimer > 0f;
	}
}