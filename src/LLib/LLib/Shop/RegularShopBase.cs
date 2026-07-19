using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using LLib.Shop.Model;

namespace LLib.Shop;

public class RegularShopBase
{
	private readonly IShopWindow _parentWindow;

	private readonly string _addonName;

	private readonly IPluginLog _pluginLog;

	private readonly IGameGui _gameGui;

	private readonly IAddonLifecycle _addonLifecycle;

	public ItemForSale? ItemForSale { get; set; }

	public PurchaseState? PurchaseState { get; private set; }

	public bool AutoBuyEnabled => PurchaseState != null;

	public bool IsAwaitingYesNo
	{
		get
		{
			return PurchaseState?.IsAwaitingYesNo ?? false;
		}
		set
		{
			PurchaseState.IsAwaitingYesNo = value;
		}
	}

	public RegularShopBase(IShopWindow parentWindow, string addonName, IPluginLog pluginLog, IGameGui gameGui, IAddonLifecycle addonLifecycle)
	{
		_parentWindow = parentWindow;
		_addonName = addonName;
		_pluginLog = pluginLog;
		_gameGui = gameGui;
		_addonLifecycle = addonLifecycle;
		_addonLifecycle.RegisterListener(AddonEvent.PostSetup, _addonName, ShopPostSetup);
		_addonLifecycle.RegisterListener(AddonEvent.PreFinalize, _addonName, ShopPreFinalize);
		_addonLifecycle.RegisterListener(AddonEvent.PostUpdate, _addonName, ShopPostUpdate);
	}

	private unsafe void ShopPostSetup(AddonEvent type, AddonArgs args)
	{
		if (!_parentWindow.IsEnabled)
		{
			ItemForSale = null;
			_parentWindow.IsOpen = false;
			return;
		}
		_parentWindow.UpdateShopStock((AtkUnitBase*)args.Addon.Address);
		PostUpdateShopStock();
		if (ItemForSale != null)
		{
			_parentWindow.IsOpen = true;
		}
	}

	private void ShopPreFinalize(AddonEvent type, AddonArgs args)
	{
		PurchaseState = null;
		_parentWindow.RestoreExternalPluginState();
		_parentWindow.IsOpen = false;
	}

	private unsafe void ShopPostUpdate(AddonEvent type, AddonArgs args)
	{
		if (!_parentWindow.IsEnabled)
		{
			ItemForSale = null;
			_parentWindow.IsOpen = false;
			return;
		}
		_parentWindow.UpdateShopStock((AtkUnitBase*)args.Addon.Address);
		PostUpdateShopStock();
		if (ItemForSale != null)
		{
			AtkUnitBase* addon = (AtkUnitBase*)args.Addon.Address;
			short x = 0;
			short y = 0;
			addon->GetPosition(&x, &y);
			ushort width = 0;
			ushort height = 0;
			addon->GetSize(&width, &height, true);
			x = (short)(x + width);
			Vector2? position = _parentWindow.Position;
			if (position.HasValue)
			{
				Vector2 position2 = position.GetValueOrDefault();
				if ((short)position2.X != x || (short)position2.Y != y)
				{
					_parentWindow.Position = new Vector2(x, y);
				}
			}
			_parentWindow.IsOpen = true;
		}
		else
		{
			_parentWindow.IsOpen = false;
		}
	}

	private void PostUpdateShopStock()
	{
		if (ItemForSale != null && PurchaseState != null)
		{
			int ownedItems = (int)ItemForSale.OwnedItems;
			if (PurchaseState.OwnedItems != ownedItems)
			{
				PurchaseState.OwnedItems = ownedItems;
				PurchaseState.NextStep = DateTime.Now.AddSeconds(0.25);
			}
		}
	}

	public unsafe int GetItemCount(uint itemId)
	{
		return InventoryManager.Instance()->GetInventoryItemCount(itemId, isHq: false, checkEquipped: false, checkArmory: false, 0);
	}

	public int GetMaxItemsToPurchase()
	{
		if (ItemForSale == null)
		{
			return 0;
		}
		return (int)(_parentWindow.GetCurrencyCount() / ItemForSale.Price);
	}

	public void CancelAutoPurchase()
	{
		PurchaseState = null;
		_parentWindow.RestoreExternalPluginState();
	}

	public void StartAutoPurchase(int toPurchase)
	{
		PurchaseState = new PurchaseState((int)ItemForSale.OwnedItems + toPurchase, (int)ItemForSale.OwnedItems);
		_parentWindow.SaveExternalPluginState();
	}

	public unsafe void HandleNextPurchaseStep()
	{
		if (ItemForSale == null || PurchaseState == null)
		{
			return;
		}
		int maxStackSize = DetermineMaxStackSize(ItemForSale.ItemId);
		if (maxStackSize == 0 && !HasFreeInventorySlot())
		{
			_pluginLog.Warning("No free inventory slots, can't buy more " + ItemForSale.ItemName);
			PurchaseState = null;
			_parentWindow.RestoreExternalPluginState();
		}
		else if (!PurchaseState.IsComplete)
		{
			if (PurchaseState.NextStep <= DateTime.Now && _gameGui.TryGetAddonByName<AtkUnitBase>(_addonName, out var addonShop))
			{
				int buyNow = Math.Min(PurchaseState.ItemsLeftToBuy, maxStackSize);
				_pluginLog.Information($"Buying {buyNow}x {ItemForSale.ItemName}");
				_parentWindow.TriggerPurchase(addonShop, buyNow);
				PurchaseState.NextStep = DateTime.MaxValue;
				PurchaseState.IsAwaitingYesNo = true;
			}
		}
		else
		{
			_pluginLog.Information($"Stopping item purchase (desired = {PurchaseState.DesiredItems}, owned = {PurchaseState.OwnedItems})");
			PurchaseState = null;
			_parentWindow.RestoreExternalPluginState();
		}
	}

	public void Dispose()
	{
		_addonLifecycle.UnregisterListener(AddonEvent.PostSetup, _addonName, ShopPostSetup);
		_addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, _addonName, ShopPreFinalize);
		_addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, _addonName, ShopPostUpdate);
	}

	public bool HasFreeInventorySlot()
	{
		return CountFreeInventorySlots() > 0;
	}

	public unsafe int CountFreeInventorySlots()
	{
		InventoryManager* inventoryManger = InventoryManager.Instance();
		if (inventoryManger == null)
		{
			return 0;
		}
		int count = 0;
		for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; t++)
		{
			InventoryContainer* container = inventoryManger->GetInventoryContainer(t);
			for (int i = 0; i < container->Size; i++)
			{
				InventoryItem* item = container->GetInventorySlot(i);
				if (item == null || item->ItemId == 0)
				{
					count++;
				}
			}
		}
		return count;
	}

	private unsafe int DetermineMaxStackSize(uint itemId)
	{
		InventoryManager* inventoryManger = InventoryManager.Instance();
		if (inventoryManger == null)
		{
			return 0;
		}
		int max = 0;
		for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; t++)
		{
			InventoryContainer* container = inventoryManger->GetInventoryContainer(t);
			for (int i = 0; i < container->Size; i++)
			{
				InventoryItem* item = container->GetInventorySlot(i);
				if (item == null || item->ItemId == 0)
				{
					return 99;
				}
				if (item->ItemId == itemId)
				{
					max += 999 - item->Quantity;
					if (max >= 99)
					{
						break;
					}
				}
			}
		}
		return Math.Min(99, max);
	}

	public unsafe int CountInventorySlotsWithCondition(uint itemId, Predicate<int> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate, "predicate");
		InventoryManager* inventoryManager = InventoryManager.Instance();
		if (inventoryManager == null)
		{
			return 0;
		}
		int count = 0;
		for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; t++)
		{
			InventoryContainer* container = inventoryManager->GetInventoryContainer(t);
			for (int i = 0; i < container->Size; i++)
			{
				InventoryItem* item = container->GetInventorySlot(i);
				if (item != null && item->ItemId != 0 && item->ItemId == itemId && predicate(item->Quantity))
				{
					count++;
				}
			}
		}
		return count;
	}
}
