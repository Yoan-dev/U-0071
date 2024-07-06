using Unity.Entities;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class ConfigAuthoring : MonoBehaviour
	{
		public Config Config;

		public class Baker : Baker<ConfigAuthoring>
		{
			public override void Bake(ConfigAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.None);

				AddComponent(entity, authoring.Config);
			}
		}
	}
}