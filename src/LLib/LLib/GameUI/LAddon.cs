using System;
using System.Linq;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace LLib.GameUI;

public static class LAddon
{
	private const int UnitListCount = 18;

	public unsafe static AtkUnitBase* GetAddonById(uint id)
	{
		AtkUnitList* unitManagers = &AtkStage.Instance()->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
		for (int i = 0; i < 18; i++)
		{
			AtkUnitList* unitManager = unitManagers + i;
			foreach (int j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->Entries.Length)))
			{
				AtkUnitBase* unitBase = unitManager->Entries[j].Value;
				if (unitBase != null && unitBase->Id == id)
				{
					return unitBase;
				}
			}
		}
		return null;
	}

	public unsafe static bool TryGetAddonByName<T>(this IGameGui gameGui, string addonName, out T* addonPtr) where T : unmanaged
	{
		ArgumentNullException.ThrowIfNull(gameGui, "gameGui");
		ArgumentException.ThrowIfNullOrEmpty(addonName, "addonName");
		AtkUnitBasePtr a = gameGui.GetAddonByName(addonName);
		if (!a.IsNull)
		{
			addonPtr = (T*)a.Address;
			return true;
		}
		addonPtr = null;
		return false;
	}

	public unsafe static bool IsAddonReady(AtkUnitBase* addon)
	{
		if (addon->IsVisible)
		{
			return addon->UldManager.LoadedState == AtkLoadState.Loaded;
		}
		return false;
	}
}
