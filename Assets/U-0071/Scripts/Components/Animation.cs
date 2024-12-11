using System;
using System.Runtime.CompilerServices;
using Unity.Entities;

namespace U0071
{
	[Serializable]
	public struct Animation
	{
		public int StartIndex;
		public int Duration;
		public float FrameTime;
		public bool Looping;
	}

	public struct AnimationController : IComponentData
	{
		public int Frame;
		public float Timer;
		public Animation Animation;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AnimationController GetDefault()
		{
			return new AnimationController
			{
				Animation = new Animation
				{
					StartIndex = -1, // allow first update
				}
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void StartAnimation(in Animation animation)
		{
			Animation = animation;
			Frame = 0;
			Timer = 0f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsPlaying(in Animation animation)
		{
			return animation.StartIndex == Animation.StartIndex;
		}
	}

	public struct SimpleAnimationTag : IComponentData { }
}