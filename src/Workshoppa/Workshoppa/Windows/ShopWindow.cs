using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.ImGui;
using LLib.Shop;
using Workshoppa.External;

namespace Workshoppa.Windows;

internal abstract class ShopWindow : LWindow, IShopWindow, IDisposable
{
	private readonly ExternalPluginHandler _externalPluginHandler;

	public bool AutoBuyEnabled => Shop.AutoBuyEnabled;

	public bool IsAwaitingYesNo
	{
		get
		{
			return Shop.IsAwaitingYesNo;
		}
		set
		{
			Shop.IsAwaitingYesNo = value;
		}
	}

	protected RegularShopBase Shop { get; }

	public abstract bool IsEnabled { get; }

	protected ShopWindow(string windowName, string addonName, IPluginLog pluginLog, IGameGui gameGui, IAddonLifecycle addonLifecycle, ExternalPluginHandler externalPluginHandler)
		: base(windowName)
	{
		_externalPluginHandler = externalPluginHandler;
		Shop = new RegularShopBase(this, addonName, pluginLog, gameGui, addonLifecycle);
		base.Position = new Vector2(100f, 100f);
		base.PositionCondition = ImGuiCond.Always;
		base.Flags = ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
	}

	public void Dispose()
	{
		Shop.Dispose();
	}

	public abstract int GetCurrencyCount();

	public unsafe abstract void UpdateShopStock(AtkUnitBase* addon);

	public unsafe abstract void TriggerPurchase(AtkUnitBase* addonShop, int buyNow);

	public void SaveExternalPluginState()
	{
		_externalPluginHandler.Save();
	}

	public void RestoreExternalPluginState()
	{
		_externalPluginHandler.Restore();
	}
}
