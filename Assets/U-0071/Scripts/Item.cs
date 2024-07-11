using System;

namespace U0071
{
	[Flags]
	public enum ItemFlag
	{
		RawFood = 1 << 0,
		Food = 1 << 1,
		Trash = 1 << 2,
		Contaminated = 1 << 3,
		All = RawFood | Food | Trash,
	}
}