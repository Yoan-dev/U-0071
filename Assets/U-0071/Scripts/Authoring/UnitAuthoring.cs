using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class UnitAuthoring : MonoBehaviour
	{
		public string Name;
		public float Speed;
		public float InteractableRange;
		public Color ShirtColor;
		public Color SkinColor;
		public Color ShortHairColor;
		public Color LongHairColor;
		public Color BeardColor;

		public class Baker : Baker<UnitAuthoring>
		{
			public override void Bake(UnitAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
					BaseYOffset = Const.CharacterYOffset,
				});
				AddComponent(entity, new MovementComponent { Speed = authoring.Speed });
				AddComponent(entity, AnimationController.GetDefault());
				AddComponent(entity, new PartitionComponent());
				AddComponent(entity, new InteractableComponent { Range = authoring.InteractableRange });
				AddComponent(entity, new PickComponent());
				SetComponentEnabled<PickComponent>(entity, false);

				// render
				AddComponent(entity, new TextureArrayIndex { Value = 0f });
				AddComponent(entity, new Orientation { Value = 1f });
				AddComponent(entity, new ShirtColor { Value = authoring.ShirtColor.linear.ToFloat4() });
				AddComponent(entity, new SkinColor { Value = authoring.SkinColor.linear.ToFloat4() });
				AddComponent(entity, new ShortHairColor { Value = authoring.ShortHairColor.linear.ToFloat4() });
				AddComponent(entity, new LongHairColor { Value = authoring.LongHairColor.linear.ToFloat4() });
				AddComponent(entity, new BeardColor { Value = authoring.BeardColor.linear.ToFloat4() });
			}
		}
	}
}