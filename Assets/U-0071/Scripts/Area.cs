using System;
using Unity.Entities;

namespace U0071
{
	[Flags]
	public enum AreaAuthorisation
	{
		LevelOne = 1 << 0,
		LevelTwo = 1 << 1,
		LevelThree = 1 << 2,
		Red = 1 << 3,
		Blue = 1 << 4,
		Yellow = 1 << 5,
		LevelTwoDown = LevelOne | LevelTwo,
		LevelThreeDown = LevelOne | LevelTwo | LevelThree,
	}

	public struct AuthorisationComponent : IComponentData
	{
		public AreaAuthorisation AreaFlag;
		public int CurrentCode;
	}
}