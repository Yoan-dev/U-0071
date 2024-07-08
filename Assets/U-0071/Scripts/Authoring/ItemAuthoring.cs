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

		[Header("Interactable")]
		public bool Pickable;
		public bool Storage;
		public float Range = 0.5f;
		public int Cost;

		[Header("Spawner")]
		public GameObject Prefab;
		public float Offset;

		[Header("Animation")]
		public bool Animated;
		public Animation Animation;

		public float2 GetFacingDirection()
		{
			float yaw = transform.rotation.eulerAngles.y;
			return
				yaw == 0f ? new float2(0f, -1f) :
				yaw == 90f ? new float2(-1f, 0f) :
				yaw == 180f ? new float2(0f, 1f) : new float2(1f, 0f);
		}

		public class Baker : Baker<ItemAuthoring>
		{
			public override void Bake(ItemAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
					BaseYOffset = authoring.Pickable ? Const.ItemYOffset : Const.DeviceYOffset,
				});
				AddComponent(entity, new PartitionComponent());

				ActionType actionType = 0;

				if (authoring.Pickable)
				{
					actionType |= ActionType.Pick;
					AddComponent(entity, new PickableComponent());
					SetComponentEnabled<PickableComponent>(entity, false);
				}
				if (authoring.Storage)
				{
					actionType |= ActionType.Trash;
				}
				if (authoring.Prefab != null)
				{
					actionType |= ActionType.Buy; // TBD: other types of spawner
					AddComponent(entity, new SpawnerComponent
					{
						Prefab = authoring.gameObject != null ? GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic) : Entity.Null,
						Offset = authoring.Offset * authoring.GetFacingDirection(),
					});
				}

				AddComponent(entity, new InteractableComponent
				{
					Flags = actionType,
					Range = authoring.Range,
					Cost = authoring.Cost,
				});

				if (authoring.Animated)
				{
					AnimationController controller = new AnimationController();
					controller.StartAnimation(authoring.Animation);
					AddComponent(entity, controller);
					AddComponent(entity, new SimpleAnimationTag());
					AddComponent(entity, new TextureArrayIndex());
				}
			}
		}
	}
}