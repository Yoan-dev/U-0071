using Unity.Entities;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class ContainerAuthoring : MonoBehaviour
	{
		public class Baker : Baker<ContainerAuthoring>
		{
			public override void Bake(ContainerAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);
			}
		}
	}
}