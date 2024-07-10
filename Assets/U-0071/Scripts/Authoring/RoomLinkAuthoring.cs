using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class RoomLinkAuthoring : MonoBehaviour
	{
		public bool IsWanderPath;

		public class Baker : Baker<RoomLinkAuthoring>
		{
			public override void Bake(RoomLinkAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.transform.position;

				AddComponent(entity, new RoomLinkComponent
				{
					Position = new float2(position.x, position.z),
					IsWanderPath = authoring.IsWanderPath,
				});
			}
		}
	}
}