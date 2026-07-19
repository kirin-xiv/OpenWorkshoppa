using System;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace LLib.GameUI;

public static class LAtkValue
{
	public unsafe static string? ReadAtkString(this AtkValue atkValue)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		if ((int)atkValue.Type == 0)
		{
			return null;
		}
		if (atkValue.String.HasValue)
		{
			return MemoryHelper.ReadSeStringNullTerminated(new IntPtr((byte*)atkValue.String)).WithCertainMacroCodeReplacements();
		}
		return null;
	}
}
