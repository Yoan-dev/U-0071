using Unity.Collections;
using Unity.Entities;

namespace U0071
{
	public struct NameComponent : IComponentData
	{
		public FixedString32Bytes Value;
	}
}