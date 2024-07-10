using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct PartitionComponent : IComponentData
	{
		public Entity CurrentRoom;
	}

	public struct RoomData
	{
		public RoomComponent Room;
		public float2 Position;
		public Entity Entity;
		
		public int Size => Room.Dimensions.x * Room.Dimensions.y;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float2 GetRoomRatio(float2 position)
		{
			return new float2
			{
				x = (position.x - (Position.x - Room.Dimensions.x / 2f)) / Room.Dimensions.x,
				y = -(position.y - (Position.y + Room.Dimensions.y / 2f)) / Room.Dimensions.y,
			};
		}
	}

	public struct Partition : IComponentData, IDisposable
	{
		public NativeArray<RoomData> Rooms;
		public NativeArray<bool> Path;
		public int2 Dimensions;

		public Partition(int2 dimensions)
		{
			Dimensions = dimensions;
			Rooms = new NativeArray<RoomData>(dimensions.x * dimensions.y, Allocator.Persistent);
			Path = new NativeArray<bool>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			Rooms.Dispose();
			Path.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomData GetRoomData(float2 position)
		{
			int index = GetIndex(position);
			return index >= 0 && index < Rooms.Length ? Rooms[index] : new RoomData();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsPathable(float2 position)
		{
			int index = GetIndex(position);
			return IsPathable(index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsPathable(int index)
		{
			return index >= 0 && index < Rooms.Length ? Path[index] : false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetCellData(bool pathable, RoomData room, float2 position)
		{
			int index = GetIndex(position);
			if (index >= 0 && index < Rooms.Length)
			{
				Rooms[index] = room;
				Path[index] = pathable;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetIndex(float2 position)
		{
			return (int)(position.x + Dimensions.x / 2) + (int)(position.y + Dimensions.y / 2) * Dimensions.x;
		}
	}
}