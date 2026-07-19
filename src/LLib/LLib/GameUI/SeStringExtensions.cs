using Dalamud.Game.Text.SeStringHandling;
using Lumina.Text.ReadOnly;

namespace LLib.GameUI;

public static class SeStringExtensions
{
	public static string WithCertainMacroCodeReplacements(this SeString? str)
	{
		if (str == null)
		{
			return string.Empty;
		}
		return new ReadOnlySeString(str.Encode()).WithCertainMacroCodeReplacements();
	}
}
