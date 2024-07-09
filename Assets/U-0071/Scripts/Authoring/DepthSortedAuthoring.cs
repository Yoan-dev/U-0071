using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class DepthSortedAuthoring : MonoBehaviour
	{
		public class Baker : Baker<DepthSortedAuthoring>
		{
			public override void Bake(DepthSortedAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				AddComponent(entity, new DepthSorted_Tag());
			}
		}
	}
}