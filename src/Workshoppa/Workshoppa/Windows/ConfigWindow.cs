using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using LLib.ImGui;

namespace Workshoppa.Windows;

internal sealed class ConfigWindow : LWindow, IPersistableWindowConfig
{
	private readonly IDalamudPluginInterface _pluginInterface;

	private readonly Configuration _configuration;

	public WindowConfig WindowConfig => _configuration.ConfigWindowConfig;

	public ConfigWindow(IDalamudPluginInterface pluginInterface, Configuration configuration)
		: base("Workshoppa - Configuration###WorkshoppaConfigWindow")
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		_pluginInterface = pluginInterface;
		_configuration = configuration;
		base.Position = new Vector2(100f, 100f);
		base.PositionCondition = ImGuiCond.FirstUseEver;
		base.Flags = ImGuiWindowFlags.AlwaysAutoResize;
		base.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(270f, 50f)
		};
	}

	public override void DrawContent()
	{
		bool enableRepairKitCalculator = _configuration.EnableRepairKitCalculator;
		if (ImGui.Checkbox("Enable Repair Kit Calculator", ref enableRepairKitCalculator))
		{
			_configuration.EnableRepairKitCalculator = enableRepairKitCalculator;
			_pluginInterface.SavePluginConfig(_configuration);
		}
		bool enableCeruleumTankCalculator = _configuration.EnableCeruleumTankCalculator;
		if (ImGui.Checkbox("Enable Ceruleum Tank Calculator", ref enableCeruleumTankCalculator))
		{
			_configuration.EnableCeruleumTankCalculator = enableCeruleumTankCalculator;
			_pluginInterface.SavePluginConfig(_configuration);
		}
	}

	public void SaveWindowConfig()
	{
		_pluginInterface.SavePluginConfig(_configuration);
	}
}
