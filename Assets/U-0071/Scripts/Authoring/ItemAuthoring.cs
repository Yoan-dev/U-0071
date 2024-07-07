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

		[Header("Animation")]
		public bool Animated;
		public Animation Animation;

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

				if (authoring.Animated)
				{
					AnimationController controller = new AnimationController();
					controller.StartAnimation(authoring.Animation);
					AddComponent(entity, controller);
					AddComponent(entity, new SimpleAnimationTag());
					AddComponent(entity, new TextureArrayIndex());
				}

				ActionType actionType = 0;
				if (authoring.Pickable)
				{
					actionType |= ActionType.Pick;
					AddComponent(entity, new PickableComponent());
					SetComponentEnabled<PickableComponent>(entity, false);
				}
				if (authoring.Storage)
				{
					actionType |= ActionType.Grind;
				}
				AddComponent(entity, new InteractableComponent
				{
					Flags = actionType,
					Range = authoring.Range,
				});
			}
		}
	}
}