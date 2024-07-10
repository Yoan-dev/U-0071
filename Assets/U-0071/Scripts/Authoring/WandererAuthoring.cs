using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class WandererAuthoring : MonoBehaviour
	{
		public float2 GetFacingDirection()
		{
			float yaw = transform.rotation.eulerAngles.y;
			return
				yaw == 0f ? new float2(0f, -1f) :
				yaw == 90f ? new float2(-1f, 0f) :
				yaw == 180f ? new float2(0f, 1f) : new float2(1f, 0f);
		}

		public class Baker : Baker<WandererAuthoring>
		{
			public override void Bake(WandererAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new WandererComponent
				{
					Position = new float2(position.x, position.z),
					Direction = authoring.GetFacingDirection(),
				});
			}
		}
	}
}