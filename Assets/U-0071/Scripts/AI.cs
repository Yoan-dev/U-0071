using System.Runtime.CompilerServices;
using Unity.Entities;

namespace U0071
{
	public struct AIController : IComponentData
	{
		public ActionTarget Target;

		public bool HasTarget => Target.Has;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsType(ActionType inType)
		{
			return Target.HasType(inType);
		}
	}
}