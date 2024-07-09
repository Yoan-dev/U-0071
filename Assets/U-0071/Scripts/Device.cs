using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	// should probably have a Device ID and a blob
	// with all its data instead of cached in components

	public struct SpawnerComponent : IComponentData
	{
		public Entity Prefab;
		public Entity VariantPrefab;
		public float2 Offset;
		public int Capacity;
		public int VariantCapacity;
		public bool Immutable;
	}

	public struct GrowComponent : IComponentData
	{
		public float Time;
		public float Timer;
		public int StageCount;
	}

	public struct StorageComponent : IComponentData
	{
		public Entity Destination;
		public Entity SecondaryDestination;
	}

	public struct HazardComponent : IComponentData
	{
		public DeathType DeathType;
		public float Range;
	}

	public struct ActionNameComponent : IComponentData
	{
		public FixedString32Bytes Value;
	}

	public struct AutoSpawnTag : IComponentData { }
}