using Unity.Entities;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class PlayerAuthoring : MonoBehaviour
	{
		public class Baker : Baker<PlayerAuthoring>
		{
			public override void Bake(PlayerAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				AddComponent(entity, new PlayerController
				{
					PrimaryAction = new ActionInfo { Key = KeyCode.E },
					SecondaryAction = new ActionInfo { Key = KeyCode.F },
				});
				AddComponent(entity, new CameraComponent());
			}
		}
	}
}