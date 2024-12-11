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
		[Header("Core")]
		public string Name;

		[Header("Interactable")]
		public float Range = 0.5f;
		public float Time;

		[Header("Ignore on Spawn")]
		public float IgnoreTime;

		[Header("Contamination")]
		public float ContaminationStrength;
		public bool OnlyContaminateCarrier;

		[Header("Item Flags")]
		public bool RawFood;
		public bool Food;
		public bool Trash;
		public bool Contaminated;

		public class Baker : Baker<ItemAuthoring>
		{
			public override void Bake(ItemAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				// core
				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
					BaseYOffset = Const.PickableYOffset,
				});
				AddComponent(entity, new PartitionInfoComponent());
				AddComponent(entity, new PickableComponent());
				SetComponentEnabled<PickableComponent>(entity, false);

				if (authoring.IgnoreTime > 0f)
				{
					AddComponent(entity, new IgnoredComponent { Timer = authoring.IgnoreTime });
				}

				// interactable
				ItemFlag itemFlags = 0;
				if (authoring.RawFood) itemFlags |= ItemFlag.RawFood;
				if (authoring.Food) itemFlags |= ItemFlag.Food;
				if (authoring.Trash) itemFlags |= ItemFlag.Trash;
				if (authoring.Contaminated) itemFlags |= ItemFlag.Contaminated;
				AddComponent(entity, new InteractableComponent
				{
					ActionFlags = ActionFlag.Pick | (authoring.Food ? ActionFlag.Eat : 0),
					ItemFlags = itemFlags,
					Range = authoring.Range,
					Time = authoring.Time,
					IsIgnored = authoring.IgnoreTime > 0f,
				});
				if (authoring.ContaminationStrength > 0f)
				{
					AddComponent(entity, new ContaminateComponent { Strength = authoring.ContaminationStrength });
					if (!authoring.OnlyContaminateCarrier)
					{
						AddComponent(entity, new ContinuousContaminationTag());
					}
				}
			}
		}
	}
}