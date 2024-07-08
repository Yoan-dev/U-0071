using Unity.Entities;
using UnityEngine;

namespace U0071
{
	[DisallowMultipleComponent]
	public class AIAuthoring : MonoBehaviour
	{
		public class Baker : Baker<AIAuthoring>
		{
			public override void Bake(AIAuthoring authoring)
			{
				Entity entity = GetEntity(TransformUsageFlags.Dynamic);

				AddComponent(entity, new AITag());
			}
		}
	}
}