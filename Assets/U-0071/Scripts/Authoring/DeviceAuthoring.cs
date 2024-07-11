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
		public float CollisionRadius = 0.25f;
		public int Cost;

		[Header("Door")]
		public AreaAuthorization AreaFlag;
		public float DoorSize;

		[Header("Action Flags")]
		public bool DestroyAction;
		public bool Teleporter;

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
		public int VisualStageCount;
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

		public float2 GetFacingDirection(GameObject gameObject)
		{
			float yaw = gameObject.transform.rotation.eulerAngles.y;
			return
				yaw == 0f ? new float2(0f, -1f) :
				yaw == 90f ? new float2(-1f, 0f) :
				yaw == 180f ? new float2(0f, 1f) : new float2(1f, 0f);
		}

		public float2 GetDoorCollision()
		{
			float yaw = gameObject.transform.rotation.eulerAngles.y;
			float scale = gameObject.transform.lossyScale.x; // assumed uniform
			return yaw == 0f || yaw == 180f ?
				new float2(scale, scale * DoorSize) :
				new float2(scale * DoorSize, scale);
		}

		public class Baker : Baker<DeviceAuthoring>
		{
			public override void Bake(DeviceAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				bool isWorkingStation = false;

				// core
				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				if (authoring.ActionName.Length > 0)
				{
					AddComponent(entity, new ActionNameComponent { Value = new FixedString32Bytes(authoring.ActionName) });
				}
				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
					BaseYOffset = authoring.AreaFlag != 0 ? Const.DoorYOffset : Const.DeviceYOffset, // doors are above items
				});
				AddComponent(entity, new DeviceTag());
				AddComponent(entity, new PartitionComponent());

				// device actions
				ActionFlag actionFlags = 0;
				if (authoring.DestroyAction)
				{
					actionFlags |= ActionFlag.Destroy;
				}
				if (authoring.Prefab != null)
				{
					// working stations are spawner of working materials
					// rather than processing station (allow better AI)
					isWorkingStation = authoring.RawFood || authoring.Trash;

					if (authoring.StartingCapacity > 0 && !authoring.AutoSpawner)
					{
						// usable from start
						actionFlags |= ActionFlag.Collect;
					}
					AddComponent(entity, new SpawnerComponent
					{
						Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
						VariantPrefab = authoring.VariantPrefab != null ? GetEntity(authoring.VariantPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
						Offset = authoring.SpawnOffset * authoring.GetFacingDirection(authoring.gameObject),
						Capacity = authoring.StartingCapacity,
						Immutable = authoring.AutoSpawner,
					});
					if (authoring.GrowTime > 0f)
					{
						AddComponent(entity, new GrowComponent
						{
							Time = authoring.GrowTime,
							StageCount = authoring.VisualStageCount,
						});
					}
					if (authoring.VisualStageCount > 0 && !authoring.Animated)
					{
						// growing/capacity stages
						AddComponent(entity, new TextureArrayIndex());

						if (authoring.GrowTime <= 0f)
						{
							AddComponent(entity, new CapacityFeedbackComponent { StageCount = authoring.VisualStageCount });
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
				if (authoring.Teleporter)
				{
					actionFlags |= ActionFlag.Teleport;
					actionFlags |= ActionFlag.Store;
					Vector3 destination = authoring.Destination.transform.position;
					AddComponent(entity, new TeleporterComponent
					{
						Destination = new float2(destination.x, destination.z) + authoring.SpawnOffset * authoring.GetFacingDirection(authoring.Destination.gameObject),
					});
				}
				if (authoring.AreaFlag != 0)
				{
					actionFlags |= ActionFlag.Open;
					AddComponent(entity, new DoorComponent
					{
						AreaFlag = authoring.AreaFlag,
						StageCount = authoring.VisualStageCount,
						Collision = authoring.GetDoorCollision(),
						CodeRequirementDirection = authoring.GetFacingDirection(authoring.gameObject), // can always open on exit
					});
					AddComponent(entity, new TextureArrayIndex());
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
					CollisionRadius = authoring.CollisionRadius,
					WorkingStationFlag = isWorkingStation,
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