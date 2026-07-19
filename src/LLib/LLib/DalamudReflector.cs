using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace LLib;

public sealed class DalamudReflector : IDisposable
{
	private readonly IDalamudPluginInterface _pluginInterface;

	private readonly IFramework _framework;

	private readonly IPluginLog _pluginLog;

	private readonly Dictionary<string, IDalamudPlugin> _pluginCache = new Dictionary<string, IDalamudPlugin>();

	private bool _pluginsChanged;

	public DalamudReflector(IDalamudPluginInterface pluginInterface, IFramework framework, IPluginLog pluginLog)
	{
		_pluginInterface = pluginInterface;
		_framework = framework;
		_pluginLog = pluginLog;
		object pm = GetPluginManager();
		pm.GetType().GetEvent("OnInstalledPluginsChanged").AddEventHandler(pm, new Action(OnInstalledPluginsChanged));
		_framework.Update += FrameworkUpdate;
	}

	public void Dispose()
	{
		_framework.Update -= FrameworkUpdate;
		object pm = GetPluginManager();
		pm.GetType().GetEvent("OnInstalledPluginsChanged").RemoveEventHandler(pm, new Action(OnInstalledPluginsChanged));
	}

	private void FrameworkUpdate(IFramework framework)
	{
		if (_pluginsChanged)
		{
			_pluginsChanged = false;
			_pluginCache.Clear();
		}
	}

	private object GetPluginManager()
	{
		return _pluginInterface.GetType().Assembly.GetType("Dalamud.Service`1", throwOnError: true).MakeGenericType(_pluginInterface.GetType().Assembly.GetType("Dalamud.Plugin.Internal.PluginManager", throwOnError: true)).GetMethod("Get")
			.Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
	}

	public bool TryGetDalamudPlugin(string internalName, [MaybeNullWhen(false)] out IDalamudPlugin instance, bool suppressErrors = false, bool ignoreCache = false)
	{
		if (!ignoreCache && _pluginCache.TryGetValue(internalName, out instance))
		{
			return true;
		}
		try
		{
			object pluginManager = GetPluginManager();
			foreach (object t in (IList)pluginManager.GetType().GetProperty("InstalledPlugins").GetValue(pluginManager))
			{
				if ((string)t.GetType().GetProperty("Name").GetValue(t) == internalName)
				{
					IDalamudPlugin plugin = (IDalamudPlugin)((t.GetType().Name == "LocalDevPlugin") ? t.GetType().BaseType : t.GetType()).GetField("instance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(t);
					if (plugin != null)
					{
						instance = plugin;
						_pluginCache[internalName] = plugin;
						return true;
					}
					if (!suppressErrors)
					{
						_pluginLog.Warning("[DalamudReflector] Found requested plugin " + internalName + " but it was null");
					}
				}
			}
			instance = null;
			return false;
		}
		catch (Exception ex)
		{
			if (!suppressErrors)
			{
				_pluginLog.Error(ex, "Can't find " + internalName + " plugin: " + ex.Message);
			}
			instance = null;
			return false;
		}
	}

	private void OnInstalledPluginsChanged()
	{
		_pluginLog.Verbose("Installed plugins changed event fired");
		_pluginsChanged = true;
	}
}
