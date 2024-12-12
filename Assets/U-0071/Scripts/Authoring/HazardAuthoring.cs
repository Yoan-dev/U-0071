using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

namespace U0071
{
	[DisallowMultipleComponent]
	public class HazardAuthoring : MonoBehaviour
	{
		public DeathType DeathType;
		public float CollisionRange = 0.25f;

		public class Baker : Baker<HazardAuthoring>
		{
			public override void Bake(HazardAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				// should be done at game init, not in authoring
				// (heat of the gamejam)
				Vector3 worldPosition = authoring.gameObject.transform.position;

				AddComponent(entity, new HazardComponent
				{
					DeathType = authoring.DeathType,
				});
				float2 position = new float2(worldPosition.x, worldPosition.z);
				float2 dimensions = new float2(authoring.CollisionRange, authoring.CollisionRange);
				AddComponent(entity, new BoundsComponent
				{
					Min = position - dimensions,
					Max = position + dimensions,
				});
			}
		}
	}
}