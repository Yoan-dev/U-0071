using Unity.Collections;
using Unity.Entities;
using UnityEngine;

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

				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new MovementComponent { Speed = authoring.Speed });
			}
		}
	}
}