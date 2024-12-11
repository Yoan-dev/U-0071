using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace U0071
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public partial struct CameraSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<CameraComponent>();
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			if (Camera.main != null && SystemAPI.TryGetSingletonEntity<CameraComponent>(out Entity entity))
			{
				// we assume the entity have a local transform
				// set camera parent position (camera holder)
				Camera.main.transform.parent.SetPositionAndRotation(SystemAPI.GetComponent<LocalTransform>(entity).Position, Quaternion.identity);
			}
		}
	}
}