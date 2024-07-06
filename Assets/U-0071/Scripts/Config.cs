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
	}
}