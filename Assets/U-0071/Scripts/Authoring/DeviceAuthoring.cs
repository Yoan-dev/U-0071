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
		public float CollisionRange = 0.25f;
		public bool Collide = true;
		public bool CanBeMultiused = false;
		public bool AdminStation = false;
		public bool CanContaminatedBeContaminated = false;
		public int Cost;

		[Header("Door")]
		public AreaAuthorization AreaFlag;
		public float DoorSize;
		public float StaysOpenTime;
		public float AnimationCubicStrength;

		[Header("Action Flags")]
		public bool DestroyAction;
		public bool Teleporter;

		[Header("Item Flags")]
		public bool RawFood;
		public bool Food;
		public bool Trash;
		public bool Contaminated;

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
			float scale = gameObject.transform.localScale.x; // assumed uniform
			return yaw == 0f || yaw == 180f ?
				new float2(scale / 2f, scale / 2f * DoorSize) :
				new float2(scale / 2f * DoorSize, scale / 2f);
		}

		public class Baker : Baker<DeviceAuthoring>
		{
			public override void Bake(DeviceAuthoring authoring)
			{
				// note to reader: please do not judge my authoring scripts
				// I usually create everything a runtime and only manage a couple of prefabs
				// the good way would have (probably) been to split in much more authoring scripts (composition rather than "type-based")

				// those with this authoring are the devices

				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 worldPosition = authoring.gameObject.transform.position;

				bool isWorkingStation = false;

				// core
				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				if (authoring.ActionName.Length > 0)
				{
					AddComponent(entity, new ActionNameComponent { Value = new FixedString32Bytes(authoring.ActionName) });
				}
				float2 position = new float2(worldPosition.x, worldPosition.z);
				AddComponent(entity, new PositionComponent
				{
					Value = position,
				});
				AddComponent(entity, new DeviceTag());
				AddComponent(entity, new PartitionInfoComponent());

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
					isWorkingStation = authoring.Prefab != null && authoring.Cost <= 0f;

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
					actionFlags |= ActionFlag.TeleportItem;
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
					float2 dimensions = authoring.GetDoorCollision();
					BoundsComponent bounds = new BoundsComponent
					{
						Min = position - dimensions,
						Max = position + dimensions,
					};
					AddComponent(entity, bounds);
					AddComponent(entity, new DoorComponent
					{
						AreaFlag = authoring.AreaFlag,
						StageCount = authoring.VisualStageCount,
						StaysOpenTime = authoring.StaysOpenTime,
						AnimationCubicStrength = authoring.AnimationCubicStrength,
						CachedBounds = bounds,
						CodeRequirementFacing = authoring.GetFacingDirection(authoring.gameObject), // can always open on exit
					});
					SetComponentEnabled<DoorComponent>(entity, false); // closed
					AddComponent(entity, new TextureArrayIndex());
					AddComponent(entity, new PeekingComponent());
				}
				if (authoring.AdminStation)
				{
					actionFlags |= ActionFlag.Administrate;
					isWorkingStation = true;
				}
				if (authoring.CanContaminatedBeContaminated)
				{
					actionFlags |= ActionFlag.Contaminate;
				}

				// interactable
				ItemFlag itemFlags = 0;
				if (authoring.RawFood) itemFlags |= ItemFlag.RawFood;
				if (authoring.Food) itemFlags |= ItemFlag.Food;
				if (authoring.Trash) itemFlags |= ItemFlag.Trash;
				if (authoring.Contaminated) itemFlags |= ItemFlag.Contaminated;
				AddComponent(entity, new InteractableComponent
				{
					ActionFlags = actionFlags,
					ItemFlags = itemFlags,
					Range = authoring.Range,
					Time = authoring.Time,
					Cost = authoring.Cost,
					WorkingStationFlag = isWorkingStation,
					CollisionRadius = authoring.Collide ? authoring.CollisionRange : 0f,
					CanBeMultiused = authoring.CanBeMultiused,
				});

				// miscellaneous
				if (authoring.DeathType > DeathType.Hunger)
				{
					AddComponent(entity, new HazardComponent
					{
						DeathType = authoring.DeathType,
					});
					if (authoring.AreaFlag == 0) // doors arleady have bounds
					{
						float2 dimensions = new float2(authoring.CollisionRange, authoring.CollisionRange);
						AddComponent(entity, new BoundsComponent
						{
							Min = position - dimensions,
							Max = position + dimensions,
						});
					}
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