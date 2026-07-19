using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace Workshoppa.External;

internal sealed class PandoraIpc
{
	private const string AutoTurnInFeature = "Auto-select Turn-ins";

	private readonly IPluginLog _pluginLog;

	private readonly ICallGateSubscriber<string, bool?> _getEnabled;

	private readonly ICallGateSubscriber<string, bool, object?> _setEnabled;

	public PandoraIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
	{
		_pluginLog = pluginLog;
		_getEnabled = pluginInterface.GetIpcSubscriber<string, bool?>("PandorasBox.GetFeatureEnabled");
		_setEnabled = pluginInterface.GetIpcSubscriber<string, bool, object>("PandorasBox.SetFeatureEnabled");
	}

	public bool? DisableIfNecessary()
	{
		try
		{
			bool? enabled = _getEnabled.InvokeFunc("Auto-select Turn-ins");
			_pluginLog.Information("Pandora's Auto-select Turn-ins is " + (enabled?.ToString() ?? "null"));
			if (enabled == true)
			{
				_setEnabled.InvokeAction("Auto-select Turn-ins", arg2: false);
			}
			return enabled;
		}
		catch (IpcNotReadyError exception)
		{
			_pluginLog.Information(exception, "Unable to read pandora state");
			return null;
		}
	}

	public void Enable()
	{
		try
		{
			_setEnabled.InvokeAction("Auto-select Turn-ins", arg2: true);
		}
		catch (IpcNotReadyError exception)
		{
			_pluginLog.Error(exception, "Unable to restore pandora state");
		}
	}
}
