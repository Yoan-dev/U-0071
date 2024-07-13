using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class RoomAuthoring : MonoBehaviour
	{
		public AreaAuthorization Area;
		public int CapacityModifier;
		public bool IsWanderPath;

		[Header("Work Info Provider")]
		public GameObject Room1;
		public GameObject Room2;

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
					Area = authoring.Area,
					IsWanderPath = authoring.IsWanderPath,
					Capacity = authoring.CapacityModifier,
				});
				AddComponent<RoomInitTag>(entity);
				
				if (authoring.Room1 != null || authoring.Room2 != null)
				{
					AddComponent(entity, new WorkInfoComponent
					{
						Room1 = authoring.Room1 != null ? GetEntity(authoring.Room1, TransformUsageFlags.Dynamic) : Entity.Null,
						Room2 = authoring.Room2 != null ? GetEntity(authoring.Room2, TransformUsageFlags.Dynamic) : Entity.Null,
					});
				}

				AddBuffer<RoomElementBufferElement>(entity);
			}
		}
	}
}