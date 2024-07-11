using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static U0071.DeviceSystem;

namespace U0071
{
	[DisallowMultipleComponent]
	public class UnitSpawnerAuthoring : MonoBehaviour
	{
		public GameObject Prefab;
		public int Count;

		public class Baker : Baker<UnitSpawnerAuthoring>
		{
			public override void Bake(UnitSpawnerAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.None);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
				});
				AddComponent(entity, new SpawnerComponent
				{
					Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
					Capacity = authoring.Count,
					Immutable = true,
				});
				AddComponent(entity, new AutoSpawnTag());
				AddComponent(entity, new AutoDestroyTag());
			}
		}
	}
}