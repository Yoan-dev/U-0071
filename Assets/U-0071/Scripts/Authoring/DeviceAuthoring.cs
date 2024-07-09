using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class DeviceAuthoring : MonoBehaviour
	{
		public string Name;
		public string ActionName;

		[Header("Interactable")]
		public float Range = 1f;
		public float Time = 0.5f;
		public int Cost;

		[Header("Action Flags")]
		public bool DestroyAction;

		[Header("Item Flags")]
		public bool RawFood;
		public bool Food;
		public bool Trash;

		[Header("Spawner")]
		public GameObject Prefab;
		public GameObject VariantPrefab;
		public float SpawnOffset;
		public int StartingCapacity;
		public float GrowTime = -1f;
		public int GrowStageCount;
		public bool AutoSpawner;

		[Header("Storage")]
		public GameObject Destination;
		public GameObject SecondaryDestination;

		[Header("Hazard")]
		public DeathType DeathType;
		public float HazardRange;

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

		public class Baker : Baker<DeviceAuthoring>
		{
			public override void Bake(DeviceAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				// core
				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
					BaseYOffset = Const.DeviceYOffset,
				});
				AddComponent(entity, new PartitionComponent());

				// device actions
				ActionFlag actionFlags = 0;
				if (authoring.DestroyAction)
				{
					actionFlags |= ActionFlag.Destroy;
				}
				if (authoring.Prefab != null)
				{
					if (authoring.StartingCapacity > 0 && !authoring.AutoSpawner)
					{
						// collectable on start
						actionFlags |= ActionFlag.Collect;
					}
					AddComponent(entity, new SpawnerComponent
					{
						Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
						VariantPrefab = authoring.VariantPrefab != null ? GetEntity(authoring.VariantPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
						Offset = authoring.SpawnOffset * authoring.GetFacingDirection(),
						Capacity = authoring.StartingCapacity,
						Immutable = authoring.AutoSpawner,
					});
					if (authoring.GrowTime > 0f)
					{
						AddComponent(entity, new GrowComponent
						{
							Time = authoring.GrowTime,
							StageCount = authoring.GrowStageCount,
						});
						if (!authoring.Animated)
						{
							// growing stages
							AddComponent(entity, new TextureArrayIndex());
						}
					}
					if (authoring.AutoSpawner)
					{
						AddComponent(entity, new AutoSpawnTag());
					}
				}
				if (authoring.Destination != null)
				{
					actionFlags |= ActionFlag.Store;
					AddComponent(entity, new StorageComponent
					{
						Destination = GetEntity(authoring.Destination, TransformUsageFlags.Dynamic),
						SecondaryDestination = authoring.SecondaryDestination != null ? GetEntity(authoring.SecondaryDestination, TransformUsageFlags.Dynamic) : Entity.Null,
					});
				}

				// interactable
				ItemFlag itemFlags = 0;
				if (authoring.RawFood) itemFlags |= ItemFlag.RawFood;
				if (authoring.Food) itemFlags |= ItemFlag.Food;
				if (authoring.Trash) itemFlags |= ItemFlag.Trash;
				AddComponent(entity, new InteractableComponent
				{
					ActionFlags = actionFlags,
					ItemFlags = itemFlags,
					Range = authoring.Range,
					Time = authoring.Time,
					Cost = authoring.Cost,
				});

				// miscellaneous
				if (authoring.HazardRange > 0f)
				{
					AddComponent(entity, new HazardComponent
					{
						DeathType = authoring.DeathType,
						Range = authoring.HazardRange,
					});
				}
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