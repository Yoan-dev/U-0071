using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace U0071
{
	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateAfter(typeof(ActionSystem))]
	[UpdateBefore(typeof(HealthSystem))]
	public partial struct ItemSystem : ISystem
	{
		private float _itemClearUserTick;

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{

			var ecbs = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

			float deltaTime = SystemAPI.Time.DeltaTime;

			state.Dependency = new IgnoredItemJob
			{
				Ecb = ecbs.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
				DeltaTime = deltaTime,
			}.ScheduleParallel(state.Dependency);

			_itemClearUserTick += deltaTime;
			if (_itemClearUserTick >= Const.ItemClearUserTickTime)
			{
				_itemClearUserTick -= Const.ItemClearUserTickTime;
				
				state.Dependency = new ItemClearUserJob
				{
					ActionControllerLookup = SystemAPI.GetComponentLookup<ActionController>(true),
				}.ScheduleParallel(state.Dependency);
			}
		}

		[BurstCompile]
		public partial struct IgnoredItemJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter Ecb;
			public float DeltaTime;

			public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref IgnoredComponent ignored, ref InteractableComponent interactable)
			{
				ignored.Timer -= DeltaTime;
				if (ignored.Timer <= 0f)
				{
					interactable.IsIgnored = false;
					interactable.Changed = true;
					Ecb.RemoveComponent<IgnoredComponent>(chunkIndex, entity);
				}
			}
		}
		
		[BurstCompile]
		public partial struct ItemClearUserJob : IJobEntity
		{
			[ReadOnly] public ComponentLookup<ActionController> ActionControllerLookup;

			public void Execute(ref InteractableComponent interactable)
			{
				// hack job to fix teleported potato still reserved by sender
				if (interactable.CurrentUser != Entity.Null &&
					ActionControllerLookup.TryGetComponent(interactable.CurrentUser, out ActionController controller) && 
					!controller.IsResolving)
				{
					interactable.CurrentUser = Entity.Null;
				}
			}
		}
	}
}