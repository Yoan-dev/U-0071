using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

namespace U0071
{
	[DisallowMultipleComponent]
	public class UnitAuthoring : MonoBehaviour
	{
		public string Name;
		public float Speed;

		public class Baker : Baker<UnitAuthoring>
		{
			public override void Bake(UnitAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent { Value = new float2(position.x, position.z) });
				AddComponent(entity, new MovementComponent { Speed = authoring.Speed });
				AddComponent(entity, new PartitionComponent());
				AddComponent(entity, new InteractableComponent());
				AddComponent(entity, new PickComponent());
				SetComponentEnabled<PickComponent>(entity, false);
			}
		}
	}
}