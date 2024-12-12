using Unity.Entities;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class AnimationAuthoring : MonoBehaviour
	{
		public bool UseSpecificAnimation;
		public Animation SpecificAnimation;

		public class Baker : Baker<AnimationAuthoring>
		{
			public override void Bake(AnimationAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				AnimationController controller = AnimationController.GetDefault();

				// looping animation (devices)
				if (authoring.UseSpecificAnimation)
				{
					controller.StartAnimation(authoring.SpecificAnimation);
					AddComponent(entity, new SimpleAnimationTag());
				}

				AddComponent(entity, controller);
				AddComponent(entity, new TextureArrayIndex { Value = 0f });
			}
		}
	}
}