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
	}
}