using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace U0071
{
	// note to reader: this is an attempt of wrapping an event system allowing parallel writing
	//
	// in producer system:
	/*
		private EventCollector<T> _eventCollector;

		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
			_eventCollector = new EventCollector<T>(SystemAPI.GetBufferLookup<T>());
		}

		[BurstCompile]
		public void OnDestroy(ref SystemState state)
		{
			_eventCollector.Dispose();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
			_actionEventCollector.Update(ref state, newCapacity); // newCapacity is most likely a query entitiy count
	
			// collect events
			state.Dependency = new SomeJob
			{
				Events = _eventCollector.Writer,
			}.ScheduleParallel(state.Dependency);
			
			// write events to buffer
			state.Dependency = _eventCollector.WriteEventsToBuffer(state.Dependency);
		}
	
		[BurstCompile]
		public partial struct SomeJob : IJobEntity
		{
			public NativeList<T>.ParallelWriter Events;

			public void Execute(...)
			{
				Events.AddNoResize(new T()); // add event
			}
		}
	*/
	//
	// in consumer system:
	// get your singleton buffer => read it => clear it

	public struct EventCollector<T> : IDisposable where T : unmanaged, IBufferElementData
	{
		private Entity _lookupEntity;
		private NativeList<T> _events;
		private BufferLookup<T> _bufferLookup;

		public NativeList<T>.ParallelWriter Writer => _events.AsParallelWriter();
		public NativeList<T> Events => _events;
		public DynamicBuffer<T> Buffer => _bufferLookup[_lookupEntity];

		public EventCollector(in BufferLookup<T> bufferLookup)
		{
			_lookupEntity = Entity.Null;
			_bufferLookup = bufferLookup;
			_events = new NativeList<T>(0, Allocator.Persistent);
		}

		public void Dispose()
		{
			_events.Dispose();
		}

		public void Update(ref SystemState state, int newCapacity)
		{
			if (_lookupEntity == Entity.Null)
			{
				_lookupEntity =	new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(state.EntityManager).GetSingletonEntity();
			}

			_bufferLookup.Update(ref state);
			if (newCapacity > _events.Capacity)
			{
				_events.SetCapacity(newCapacity);
			}
			_events.Clear();
		}

		public JobHandle WriteEventsToBuffer(in JobHandle dependency)
		{
			return new WriteEventsToBufferJob<T> { Collector = this }.Schedule(dependency);
		}
	}

	[BurstCompile]
	public partial struct WriteEventsToBufferJob<T> : IJob where T : unmanaged, IBufferElementData
	{
		public EventCollector<T> Collector;

		public void Execute()
		{
			DynamicBuffer<T> buffer = Collector.Buffer;
			for (int i = 0; i < Collector.Events.Length; i++)
			{
				buffer.Add(Collector.Events[i]);
			}
		}
	}
}