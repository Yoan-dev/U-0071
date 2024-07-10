using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class CorridorOverrideAuthoring : MonoBehaviour
	{
		public class Baker : Baker<CorridorOverrideAuthoring>
		{
			public override void Bake(CorridorOverrideAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.None);

				Transform transform = authoring.gameObject.transform;
				Vector3 position = transform.position;
				Vector3 scale = transform.lossyScale;

				AddComponent(entity, new CorridorOverrideComponent
				{
					Position = new float2(position.x, position.z),
					Dimensions = transform.rotation.eulerAngles.y == 0f || transform.rotation.eulerAngles.y == 180f ? new int2((int)scale.x, (int)scale.y) : new int2((int)scale.y, (int)scale.x),
				});
			}
		}
	}
}