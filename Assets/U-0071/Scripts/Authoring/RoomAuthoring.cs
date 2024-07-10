using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class RoomAuthoring : MonoBehaviour
	{
		public bool IsWanderPath;

		public class Baker : Baker<RoomAuthoring>
		{
			public override void Bake(RoomAuthoring authoring)
			{
				// note: rooms shoudld never be rotated (not supported currently)
				// inverse x and y scales to "rotate" it

				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Transform transform = authoring.gameObject.transform;
				Vector3 position = transform.position;
				Vector3 scale = transform.lossyScale;

				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new Unity.Collections.FixedString32Bytes("Room") });
				AddComponent(entity, new PositionComponent { Value = new float2(position.x, position.z) });
				AddComponent(entity, new RoomComponent
				{
					Dimensions = transform.rotation.eulerAngles.y == 0f || transform.rotation.eulerAngles.y == 180f ? new int2((int)scale.x, (int)scale.y) : new int2((int)scale.y, (int)scale.x),
					IsWanderPath = authoring.IsWanderPath,
				});
				AddComponent<RoomInitTag>(entity);

				AddBuffer<RoomElementBufferElement>(entity);
			}
		}
	}
}