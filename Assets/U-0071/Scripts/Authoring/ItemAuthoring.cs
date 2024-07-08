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
		public string Name;

		[Header("Interactable")]
		public bool Pickable;
		public bool Processable;
		public bool Eatable;
		public bool Trasher;
		public float Range = 0.5f;
		public float Time;
		public int Cost;

		[Header("Spawner")]
		public GameObject Prefab;
		public GameObject VariantPrefab;
		public float Offset;
		public int StartingCapacity;
		public float GrowTime = -1f;
		public int GrowStageCount;
		public bool AutoSpawner;

		[Header("Storage")]
		public GameObject Destination;
		public GameObject SecondaryDestination;

		[Header("Companion Flags")]
		public bool RefTrash;
		public bool RefProcess;
		public bool RefEat;

		[Header("Animation")]
		public bool Animated;
		public Animation Animation;

		public float2 GetFacingDirection()
		{
			float yaw = transform.rotation.eulerAngles.y;
			return
				yaw == 0f ? new float2(0f, -1f) :
				yaw == 90f ? new float2(-1f, 0f) :
				yaw == 180f ? new float2(0f, 1f) : new float2(1f, 0f);
		}

		public class Baker : Baker<ItemAuthoring>
		{
			public override void Bake(ItemAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
					BaseYOffset = authoring.Pickable ? Const.PickableYOffset : Const.DeviceYOffset,
				});
				AddComponent(entity, new PartitionComponent());

				ActionType actionType = 0;

				if (authoring.Pickable)
				{
					actionType |= ActionType.Pick;
					AddComponent(entity, new PickableComponent());
					SetComponentEnabled<PickableComponent>(entity, false);
				}
				if (authoring.Eatable)
				{
					actionType |= ActionType.Eat;
				}
				if (authoring.Processable)
				{
					actionType |= ActionType.Process;
				}
				if (authoring.Trasher)
				{
					actionType |= ActionType.Trash;
				}
				if (authoring.Prefab != null)
				{
					if (authoring.StartingCapacity > 0 && !authoring.AutoSpawner)
					{
						// collectable on start
						actionType |= ActionType.Collect;
					}
					AddComponent(entity, new SpawnerComponent
					{
						Prefab = authoring.Prefab != null ? GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic) : Entity.Null,
						VariantPrefab = authoring.VariantPrefab != null ? GetEntity(authoring.VariantPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
						Offset = authoring.Offset * authoring.GetFacingDirection(),
						Capacity = authoring.StartingCapacity,
					});
					if (authoring.AutoSpawner)
					{
						AddComponent(entity, new AutoSpawnTag());
					}
					else if (authoring.GrowTime > 0f)
					{
						AddComponent(entity, new GrowComponent
						{
							Time = authoring.GrowTime,
							StageCount = authoring.GrowStageCount,
						});
						AddComponent(entity, new TextureArrayIndex());
					}
				}
				if (authoring.Destination != null)
				{
					actionType |= ActionType.Store;
					AddComponent(entity, new StorageComponent
					{
						Destination = GetEntity(authoring.Destination, TransformUsageFlags.Dynamic),
						SecondaryDestination = authoring.SecondaryDestination != null ? GetEntity(authoring.SecondaryDestination, TransformUsageFlags.Dynamic) : Entity.Null,
					});
				}

				// companion flags
				if (authoring.RefTrash) actionType |= ActionType.RefTrash;
				if (authoring.RefProcess) actionType |= ActionType.RefProcess;
				if (authoring.RefEat) actionType |= ActionType.RefEat;


				AddComponent(entity, new InteractableComponent
				{
					Flags = actionType,
					Range = authoring.Range,
					Time = authoring.Time,
					Cost = authoring.Cost,
					Immutable = authoring.AutoSpawner,
				});

				if (authoring.Animated)
				{
					AnimationController controller = new AnimationController();
					controller.StartAnimation(authoring.Animation);
					AddComponent(entity, controller);
					AddComponent(entity, new SimpleAnimationTag());
					AddComponent(entity, new TextureArrayIndex());
				}
			}
		}
	}
}