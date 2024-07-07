using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{

	[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
	[UpdateBefore(typeof(MovementSystem))]
	public partial struct AIControllerSystem : ISystem
	{
		private BufferLookup<PickDropEventBufferElement> _actionEventLookup;
		private BufferLookup<RoomElementBufferElement> _roomElementLookup;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<PickDropEventBufferElement>();

			_actionEventLookup = state.GetBufferLookup<PickDropEventBufferElement>();
			_roomElementLookup = state.GetBufferLookup<RoomElementBufferElement>(true);
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_actionEventLookup.Update(ref state);
			_roomElementLookup.Update(ref state);

			state.Dependency = new AIActionJob
			{
				LookupEntity = SystemAPI.GetSingletonEntity<PickDropEventBufferElement>(),
				ActionEventBufferLookup = _actionEventLookup,
				RoomElementBufferLookup = _roomElementLookup,
			}.ScheduleParallel(state.Dependency);

			state.Dependency = new AIMovementJob().ScheduleParallel(state.Dependency);
		}

		[BurstCompile]
		public partial struct AIMovementJob : IJobEntity
		{
			public void Execute(ref MovementComponent movement, in PlayerController controller)
			{
				movement.Input = controller.MoveInput;
			}
		}

		[BurstCompile]
		[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
		public partial struct AIActionJob : IJobEntity
		{
			public Entity LookupEntity;
			public BufferLookup<PickDropEventBufferElement> ActionEventBufferLookup;
			[ReadOnly]
			public BufferLookup<RoomElementBufferElement> RoomElementBufferLookup;
			[ReadOnly]
			public ComponentLookup<NameComponent> NameLookup;

			public void Execute(
				Entity entity,
				ref AIController ai,
				ref ActionController controller,
				in PositionComponent position,
				in PickComponent pick,
				in PartitionComponent partition)
			{
				if (partition.CurrentRoom == Entity.Null) return;

				// TODO
			}
		}
	}
}