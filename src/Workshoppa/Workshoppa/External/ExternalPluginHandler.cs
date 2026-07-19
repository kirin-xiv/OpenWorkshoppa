using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Workshoppa.External;

internal sealed class ExternalPluginHandler
{
	private readonly IDalamudPluginInterface _pluginInterface;

	private readonly IPluginLog _pluginLog;

	private readonly PandoraIpc _pandoraIpc;

	private bool? _pandoraState;

	public bool Saved { get; private set; }

	public ExternalPluginHandler(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
	{
		_pluginInterface = pluginInterface;
		_pluginLog = pluginLog;
		_pandoraIpc = new PandoraIpc(pluginInterface, pluginLog);
	}

	public void Save()
	{
		if (Saved)
		{
			_pluginLog.Information("Not overwriting external plugin state");
			return;
		}
		_pluginLog.Information("Saving external plugin state...");
		SaveYesAlreadyState();
		SavePandoraState();
		Saved = true;
	}

	private void SaveYesAlreadyState()
	{
		if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out HashSet<string> data) && !data.Contains("Workshoppa"))
		{
			_pluginLog.Debug("Disabling YesAlready");
			data.Add("Workshoppa");
		}
	}

	private void SavePandoraState()
	{
		_pandoraState = _pandoraIpc.DisableIfNecessary();
		_pluginLog.Information($"Previous pandora feature state: {_pandoraState}");
	}

	public void SaveTextAdvance()
	{
		if (_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out HashSet<string> data) && !data.Contains("Workshoppa"))
		{
			_pluginLog.Debug("Disabling textadvance");
			data.Add("Workshoppa");
		}
	}

	public void Restore()
	{
		if (Saved)
		{
			RestoreYesAlready();
			RestorePandora();
		}
		Saved = false;
		_pandoraState = null;
	}

	private void RestoreYesAlready()
	{
		if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out HashSet<string> data) && data.Contains("Workshoppa"))
		{
			_pluginLog.Debug("Restoring YesAlready");
			data.Remove("Workshoppa");
		}
	}

	private void RestorePandora()
	{
		_pluginLog.Information($"Restoring previous pandora state: {_pandoraState}");
		if (_pandoraState == true)
		{
			_pandoraIpc.Enable();
		}
	}

	public void RestoreTextAdvance()
	{
		if (_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out HashSet<string> data) && data.Contains("Workshoppa"))
		{
			_pluginLog.Debug("Restoring textadvance");
			data.Remove("Workshoppa");
		}
	}
}
