using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class WanderPointAuthoring : MonoBehaviour
	{
		public class Baker : Baker<WanderPointAuthoring>
		{
			public override void Bake(WanderPointAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.None);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new WanderPointComponent
				{
					Position = new float2(position.x, position.z),
				});
			}
		}
	}
}