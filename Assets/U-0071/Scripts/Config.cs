using System;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	[Serializable]
	public struct Config : IComponentData
	{
		public int2 WorldDimensions;
		public uint Seed;
		public Animation CharacterIdle;
		public Animation CharacterWalk;
		public Animation CharacterInteract;
		public Animation CharacterDie;
		public Animation CharacterCrushed;
	}
}