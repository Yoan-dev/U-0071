using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace U0071
{
	public struct PartitionInfoComponent : IComponentData
	{
		public Entity CurrentRoom; // room the entitiy is currently in
		public float ClosestEdgeX; // cached for drop offset
	}

	public struct RoomData
	{
		public RoomComponent Room;
		public float2 Position;
		public Entity Entity;

		public int Size => Room.Dimensions.x * Room.Dimensions.y;
		public AreaAuthorization AreaFlag => Room.Authorization;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float2 GetRoomRatio(float2 position)
		{
			return new float2
			{
				x = (position.x - (Position.x - Room.Dimensions.x / 2f)) / Room.Dimensions.x,
				y = -(position.y - (Position.y + Room.Dimensions.y / 2f)) / Room.Dimensions.y,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float GetClosestEdgeX(float x)
		{
			float left = Position.x - Room.Dimensions.x / 2f;
			float right = Position.x + Room.Dimensions.x / 2f;
			return x - left < right - x ? left : right;
		}
	}

	public struct Partition : IComponentData, IDisposable
	{
		public NativeArray<RoomData> RoomCells; // cached room data for each cell
		public NativeArray<bool> Path;
		public int2 Dimensions;

		public Partition(int2 dimensions)
		{
			Dimensions = dimensions;
			RoomCells = new NativeArray<RoomData>(dimensions.x * dimensions.y, Allocator.Persistent);
			Path = new NativeArray<bool>(dimensions.x * dimensions.y, Allocator.Persistent);
		}

		public void Dispose()
		{
			RoomCells.Dispose();
			Path.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomData GetRoomData(float2 position)
		{
			return GetRoomData(GetIndex(position));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RoomData GetRoomData(int index)
		{
			return index >= 0 && index < RoomCells.Length ? RoomCells[index] : new RoomData();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsPathable(float2 position)
		{
			return IsPathable(GetIndex(position));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsPathable(int index)
		{
			return index >= 0 && index < RoomCells.Length ? Path[index] : false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public AreaAuthorization GetAuthorization(float2 position)
		{
			return GetAuthorization(GetIndex(position));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public AreaAuthorization GetAuthorization(int index)
		{
			return index >= 0 && index < RoomCells.Length ? RoomCells[index].AreaFlag : 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetCellData(bool pathable, RoomData room, int index)
		{
			if (index >= 0 && index < RoomCells.Length)
			{
				RoomCells[index] = room;
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