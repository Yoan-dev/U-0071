using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class ConfigAuthoring : MonoBehaviour
	{
		[Header("World")]
		public int2 WorldDimensions;
		public uint Seed;

		[Header("Animations")]
		public Animation CharacterIdle;
		public Animation CharacterWalk;
		public Animation CharacterInteract;
		public Animation CharacterDie;
		public Animation CharacterCrushed;
		public Animation CharacterFlee;

		[Header("Unit Rendering")]
		public Color[] SkinColors;
		public Color[] HairColors;
		public float ChanceOfShortHair;
		public float ChanceOfLongHair;
		public float ChanceOfBeard;

		[Header("Miscellaneous")]
		public GameObject OrganicWastePrefab;

		public class Baker : Baker<ConfigAuthoring>
		{
			public override void Bake(ConfigAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.None);

				Config config = new Config
				{
					WorldDimensions = authoring.WorldDimensions,
					Seed = authoring.Seed,
					CharacterIdle = authoring.CharacterIdle,
					CharacterWalk = authoring.CharacterWalk,
					CharacterCrushed = authoring.CharacterCrushed,
					CharacterDie = authoring.CharacterDie,
					CharacterInteract = authoring.CharacterInteract,
					CharacterFlee = authoring.CharacterFlee,
					ChanceOfShortHair = authoring.ChanceOfShortHair,
					ChanceOfLongHair = authoring.ChanceOfLongHair,
					ChanceOfBeard = authoring.ChanceOfBeard,
					OrganicWastePrefab = GetEntity(authoring.OrganicWastePrefab, TransformUsageFlags.Dynamic),
				};

				var builder = new BlobBuilder(Allocator.Temp);
				ref UnitRenderingColors unitRenderingColors = ref builder.ConstructRoot<UnitRenderingColors>();

				BlobBuilderArray<float4> skinColorArrayBuilder = builder.Allocate(ref unitRenderingColors.SkinColors, authoring.SkinColors.Length);
				for (int i = 0; i < authoring.SkinColors.Length; i++)
				{
					skinColorArrayBuilder[i] = authoring.SkinColors[i].linear.ToFloat4();
				}
				BlobBuilderArray<float4> hairColorArrayBuilder = builder.Allocate(ref unitRenderingColors.HairColors, authoring.HairColors.Length);
				for (int i = 0; i < authoring.HairColors.Length; i++)
				{
					hairColorArrayBuilder[i] = authoring.HairColors[i].linear.ToFloat4();
				}
				config.UnitRenderingColors = builder.CreateBlobAssetReference<UnitRenderingColors>(Allocator.Persistent);
				builder.Dispose();

				AddComponent(entity, config);
			}
		}
	}
}