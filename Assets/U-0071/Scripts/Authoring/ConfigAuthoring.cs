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
		public float CycleDuration;

		[Header("Ending")]
		public bool HaveEnding = true;
		public float EndingPhaseOneY;
		public float EndingPhaseTwoY;
		public float EndingPhaseThreeY;
		public float EndingPhaseThreeAbsX;
		public GameObject LastDoor;

		[Header("Animations")]
		public Animation CharacterIdle;
		public Animation CharacterWalk;
		public Animation CharacterInteract;
		public Animation CharacterDie;
		public Animation CharacterCrushed;
		public Animation CharacterCrushedFromBelow;
		public Animation CharacterFlee;
		public Animation CharacterDepixelate;

		[Header("Unit Rendering")]
		public Color[] SkinColors;
		public Color[] HairColors;
		public Color LevelOneShirtColor;
		public Color LevelTwoShirtColor;
		public Color LevelThreeShirtColor;
		public Color AdminShirtColor;
		public float ChanceOfShortHair;
		public float ChanceOfLongHair;
		public float ChanceOfBeard;

		[Header("Respawn Random")]
		public int UnitArrayMaxIndex;

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
					CycleDuration = authoring.CycleDuration,
					CharacterIdle = authoring.CharacterIdle,
					CharacterWalk = authoring.CharacterWalk,
					CharacterCrushed = authoring.CharacterCrushed,
					CharacterDie = authoring.CharacterDie,
					CharacterInteract = authoring.CharacterInteract,
					CharacterFlee = authoring.CharacterFlee,
					CharacterCrushedFromBelow = authoring.CharacterCrushedFromBelow,
					ChanceOfShortHair = authoring.ChanceOfShortHair,
					ChanceOfLongHair = authoring.ChanceOfLongHair,
					ChanceOfBeard = authoring.ChanceOfBeard,
					OrganicWastePrefab = GetEntity(authoring.OrganicWastePrefab, TransformUsageFlags.Dynamic),
					LevelOneShirtColor = authoring.LevelOneShirtColor.linear.ToFloat4(),
					LevelTwoShirtColor = authoring.LevelTwoShirtColor.linear.ToFloat4(),
					LevelThreeShirtColor = authoring.LevelThreeShirtColor.linear.ToFloat4(),
					AdminShirtColor = authoring.AdminShirtColor.linear.ToFloat4(),
					UnitArrayMaxIndex = authoring.UnitArrayMaxIndex,
				};
				if (authoring.HaveEnding)
				{
					AddComponent(entity, new Ending
					{
						CharacterDepixelate = authoring.CharacterDepixelate,
						EndingPhaseOneY = authoring.EndingPhaseOneY,
						EndingPhaseTwoY = authoring.EndingPhaseTwoY,
						EndingPhaseThreeY = authoring.EndingPhaseThreeY,
						EndingPhaseThreeAbsX = authoring.EndingPhaseThreeAbsX,
						LastDoorEntity = GetEntity(authoring.LastDoor, TransformUsageFlags.Dynamic),
					});
				}

				AddComponent(entity, config);
				AddComponent(entity, new CycleComponent());
				AddComponent(entity, new GameInitFlag());
			}
		}
	}
}