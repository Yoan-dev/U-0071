using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class UnitAuthoring : MonoBehaviour
	{
		[Header("Core")]
		public string Name;
		public float Speed;
		public float Hunger = 10f;
		public AreaAuthorization Authorisation;
		public int Credits;

		[Header("Interactable")]
		public float CollisionRadius = 0.2f;
		public float Range = 0.5f;
		public float Time = 1f;

		[Header("Contamination")]
		public float ContaminationStrength;
		
		[Header("Render")]
		public Color ShirtColor;
		public Color SkinColor;
		public Color HairColor;
		public bool HasShortHair;
		public bool HasLongHair;
		public bool HasBeard;

		public class Baker : Baker<UnitAuthoring>
		{
			public override void Bake(UnitAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				Vector3 position = authoring.gameObject.transform.position;

				// core
				AddComponent(entity, new DepthSorted_Tag());
				AddComponent(entity, new NameComponent { Value = new FixedString32Bytes(authoring.Name) });
				AddComponent(entity, new PositionComponent
				{
					Value = new float2(position.x, position.z),
					BaseYOffset = Const.CharacterYOffset,
					BaseZOffset = Const.CharacterZOffset,
				});
				AddComponent(entity, new MovementComponent { Speed = authoring.Speed });
				AddComponent(entity, AnimationController.GetDefault());
				AddComponent(entity, new PartitionInfoComponent());
				AddComponent(entity, new ActionController());
				AddComponent(entity, new IsActing());
				SetComponentEnabled<IsActing>(entity, false);
				AddComponent(entity, new InteractableComponent
				{
					Range = authoring.Range,
					Time = authoring.Time,
					ActionFlags = ActionFlag.Push,
					CollisionRadius = authoring.CollisionRadius,
					CanBeMultiused = true,
				});
				AddComponent(entity, new CreditsComponent { Value = authoring.Credits });
				AddComponent(entity, new CarryComponent());
				SetComponentEnabled<CarryComponent>(entity, false);

				// health
				AddComponent(entity, new HungerComponent { Value = authoring.Hunger });
				AddComponent(entity, new DeathComponent());
				AddComponent(entity, new ResolveDeathTag());
				SetComponentEnabled<DeathComponent>(entity, false);
				AddComponent(entity, new PushedComponent());
				SetComponentEnabled<PushedComponent>(entity, false);
				AddComponent(entity, new SickComponent());
				SetComponentEnabled<SickComponent>(entity, false);
				AddComponent(entity, new ContaminationLevelComponent());
				AddComponent(entity, new ContaminateComponent { Strength = authoring.ContaminationStrength });

				// areas
				AddComponent(entity, new AuthorizationComponent
				{
					Flag = authoring.Authorisation,
				});

				// render
				AddComponent(entity, new TextureArrayIndex { Value = 0f });
				AddComponent(entity, new Orientation { Value = 1f });
				AddComponent(entity, new ShirtColor { Value = authoring.ShirtColor.linear.ToFloat4() });
				AddComponent(entity, new SkinColor { Value = authoring.SkinColor.linear.ToFloat4() });
				AddComponent(entity, new ShortHairColor { Value = (authoring.HasShortHair ? authoring.HairColor : authoring.SkinColor).linear.ToFloat4() });
				AddComponent(entity, new LongHairColor { Value = (authoring.HasLongHair ? authoring.HairColor : authoring.SkinColor).linear.ToFloat4() });
				AddComponent(entity, new BeardColor { Value = (authoring.HasBeard ? authoring.HairColor : authoring.SkinColor).linear.ToFloat4() });
				AddComponent(entity, new PilosityComponent
				{
					HasShortHair = authoring.HasShortHair,
					HasLongHair = authoring.HasLongHair,
					HasBeard = authoring.HasBeard,
				});
			}
		}
	}
}