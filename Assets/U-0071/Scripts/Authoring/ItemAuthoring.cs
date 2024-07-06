using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class ItemAuthoring : MonoBehaviour
	{
		public string Name;
		public bool Pickable;

		public class Baker : Baker<ItemAuthoring>
		{
			public override void Bake(ItemAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent { Value = new float2(position.x, position.z) });
				AddComponent(entity, new PartitionComponent());

				ActionType actionType = 0;
				if (authoring.Pickable)
				{
					actionType |= ActionType.Pick;
					AddComponent(entity, new PickedTag());
					SetComponentEnabled<PickedTag>(entity, false);
				}
				AddComponent(entity, new InteractableComponent { Type = actionType });
			}
		}
	}
}