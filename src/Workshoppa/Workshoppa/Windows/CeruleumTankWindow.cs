using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using LLib.Shop.Model;
using Workshoppa.External;

namespace Workshoppa.Windows;

internal sealed class CeruleumTankWindow : ShopWindow
{
	private const int CeruleumTankItemId = 10155;

	private readonly IPluginLog _pluginLog;

	private readonly Configuration _configuration;

	private readonly IChatGui _chatGui;

	private int _companyCredits;

	private int _buyStackCount;

	private bool _buyPartialStacks = true;

	public override bool IsEnabled => _configuration.EnableCeruleumTankCalculator;

	public CeruleumTankWindow(IPluginLog pluginLog, IGameGui gameGui, IAddonLifecycle addonLifecycle, Configuration configuration, ExternalPluginHandler externalPluginHandler, IChatGui chatGui)
		: base("Ceruleum Tanks###WorkshoppaCeruleumTankWindow", "FreeCompanyCreditShop", pluginLog, gameGui, addonLifecycle, externalPluginHandler)
	{
		_pluginLog = pluginLog;
		_chatGui = chatGui;
		_configuration = configuration;
	}

	public unsafe override void UpdateShopStock(AtkUnitBase* addon)
	{
		if (addon->AtkValuesCount != 170)
		{
			_pluginLog.Error($"Unexpected amount of atkvalues for FreeCompanyCreditShop addon ({addon->AtkValuesCount})");
			_companyCredits = 0;
			base.Shop.ItemForSale = null;
			return;
		}
		AtkValue* atkValues = addon->AtkValues;
		_companyCredits = (int)atkValues[3].UInt;
		uint itemCount = atkValues[9].UInt;
		if (itemCount == 0)
		{
			base.Shop.ItemForSale = null;
			return;
		}
		base.Shop.ItemForSale = (from i in Enumerable.Range(0, (int)itemCount)
			select new ItemForSale
			{
				Position = i,
				ItemName = atkValues[10 + i].ReadAtkString(),
				Price = atkValues[130 + i].UInt,
				OwnedItems = atkValues[90 + i].UInt,
				ItemId = atkValues[30 + i].UInt
			}).FirstOrDefault((ItemForSale x) => x.ItemId == 10155);
	}

	public override int GetCurrencyCount()
	{
		return _companyCredits;
	}

	public override void DrawContent()
	{
		if (base.Shop.ItemForSale == null)
		{
			base.IsOpen = false;
			return;
		}
		int ceruleumTanks = base.Shop.GetItemCount(10155u);
		int freeInventorySlots = base.Shop.CountFreeInventorySlots();
		ImGui.Text("Inventory");
		ImGui.Indent();
		ImU8String text = new ImU8String(16, 1);
		text.AppendLiteral("Ceruleum Tanks: ");
		text.AppendFormatted(FormatStackCount(ceruleumTanks));
		ImGui.Text(text);
		ImU8String text2 = new ImU8String(12, 1);
		text2.AppendLiteral("Free Slots: ");
		text2.AppendFormatted(freeInventorySlots);
		ImGui.Text(text2);
		ImGui.Unindent();
		ImGui.Separator();
		if (base.Shop.PurchaseState == null)
		{
			ImGui.SetNextItemWidth(100f);
			ImGui.InputInt("Stacks to Buy", ref _buyStackCount);
			_buyStackCount = Math.Min(freeInventorySlots, Math.Max(0, _buyStackCount));
			if (ceruleumTanks % 999 > 0)
			{
				ImU8String label = new ImU8String(23, 1);
				label.AppendLiteral("Fill Partial Stacks (+");
				label.AppendFormatted(999 - ceruleumTanks % 999);
				label.AppendLiteral(")");
				ImGui.Checkbox(label, ref _buyPartialStacks);
			}
		}
		int missingItems = _buyStackCount * 999;
		if (_buyPartialStacks && ceruleumTanks % 999 > 0)
		{
			missingItems += 999 - ceruleumTanks % 999;
		}
		if (base.Shop.PurchaseState != null)
		{
			base.Shop.HandleNextPurchaseStep();
			if (base.Shop.PurchaseState != null)
			{
				ImU8String text3 = new ImU8String(10, 1);
				text3.AppendLiteral("Buying ");
				text3.AppendFormatted(FormatStackCount(base.Shop.PurchaseState.ItemsLeftToBuy));
				text3.AppendLiteral("...");
				ImGui.Text(text3);
				if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel Auto-Buy"))
				{
					base.Shop.CancelAutoPurchase();
				}
			}
			return;
		}
		int toPurchase = Math.Min(base.Shop.GetMaxItemsToPurchase(), missingItems);
		if (toPurchase > 0)
		{
			ImGui.Spacing();
			if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.DollarSign, $"Auto-Buy {FormatStackCount(toPurchase)} for {base.Shop.ItemForSale.Price * toPurchase:N0} CC"))
			{
				base.Shop.StartAutoPurchase(toPurchase);
				base.Shop.HandleNextPurchaseStep();
			}
		}
	}

	private static string FormatStackCount(int ceruleumTanks)
	{
		int fullStacks = ceruleumTanks / 999;
		int partials = ceruleumTanks % 999;
		string stacks = ((fullStacks == 1) ? "stack" : "stacks");
		if (partials <= 0)
		{
			return $"{fullStacks:N0} {stacks}";
		}
		return $"{fullStacks:N0} {stacks} + {partials}";
	}

	public unsafe override void TriggerPurchase(AtkUnitBase* addonShop, int buyNow)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		AtkValue* buyItem = stackalloc AtkValue[3]
		{
			new AtkValue
			{
				Type = (AtkValueType)3,
				Int = 0
			},
			new AtkValue
			{
				Type = (AtkValueType)5,
				UInt = (uint)base.Shop.ItemForSale.Position
			},
			new AtkValue
			{
				Type = (AtkValueType)5,
				UInt = (uint)buyNow
			}
		};
		addonShop->FireCallback(3u, buyItem);
	}

	public bool TryParseBuyRequest(string arguments, out int missingQuantity)
	{
		if (!int.TryParse(arguments, out var stackCount) || stackCount <= 0)
		{
			missingQuantity = 0;
			return false;
		}
		stackCount = Math.Min(base.Shop.CountFreeInventorySlots(), stackCount);
		missingQuantity = Math.Min(base.Shop.GetMaxItemsToPurchase(), stackCount * 999);
		return true;
	}

	public bool TryParseFillRequest(string arguments, out int missingQuantity)
	{
		if (!int.TryParse(arguments, out var stackCount) || stackCount < 0)
		{
			missingQuantity = 0;
			return false;
		}
		int freeInventorySlots = base.Shop.CountFreeInventorySlots();
		int partialStacks = base.Shop.CountInventorySlotsWithCondition(10155u, (int q) => q < 999);
		int fullStacks = base.Shop.CountInventorySlotsWithCondition(10155u, (int q) => q == 999);
		int tanks = Math.Min((fullStacks + partialStacks + freeInventorySlots) * 999, Math.Max(stackCount * 999, (fullStacks + partialStacks) * 999));
		_pluginLog.Information("T: " + tanks);
		int owned = base.Shop.GetItemCount(10155u);
		if (tanks <= owned)
		{
			missingQuantity = 0;
		}
		else
		{
			missingQuantity = Math.Min(base.Shop.GetMaxItemsToPurchase(), tanks - owned);
		}
		return true;
	}

	public void StartPurchase(int quantity)
	{
		if (!base.IsOpen || base.Shop.ItemForSale == null)
		{
			_chatGui.PrintError("Could not start purchase, shop window is not open.");
			return;
		}
		if (quantity <= 0)
		{
			_chatGui.Print("Not buying ceruleum tanks, you already have enough.");
			return;
		}
		_chatGui.Print("Starting purchase of " + FormatStackCount(quantity) + " ceruleum tanks.");
		base.Shop.StartAutoPurchase(quantity);
		base.Shop.HandleNextPurchaseStep();
	}
}
