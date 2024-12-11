using System.Runtime.CompilerServices;
using Unity.Entities;

namespace U0071
{
	public struct CycleComponent : IComponentData
	{
		public int LevelOneCode;
		public int LevelTwoCode;
		public int LevelThreeCode;
		public int RedCode;
		public int BlueCode;
		public int YellowCode;
		public float CycleTimer;
		public uint CycleCounter;
		public bool CycleChanged;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetCode(AreaAuthorization authorization)
		{
			return authorization switch
			{
				AreaAuthorization.LevelOne => LevelOneCode,
				AreaAuthorization.LevelTwo => LevelTwoCode,
				AreaAuthorization.LevelThree => LevelThreeCode,
				AreaAuthorization.Red => RedCode,
				AreaAuthorization.Blue => BlueCode,
				AreaAuthorization.Yellow => YellowCode,
				_ => 1234,
			};
		}
	}
}