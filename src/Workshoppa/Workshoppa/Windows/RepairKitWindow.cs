using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using LLib.Shop.Model;
using Workshoppa.External;

namespace Workshoppa.Windows;

internal sealed class RepairKitWindow : ShopWindow
{
	private const int DarkMatterCluster6ItemId = 10386;

	private readonly IPluginLog _pluginLog;

	private readonly Configuration _configuration;

	public override bool IsEnabled => _configuration.EnableRepairKitCalculator;

	public RepairKitWindow(IPluginLog pluginLog, IGameGui gameGui, IAddonLifecycle addonLifecycle, Configuration configuration, ExternalPluginHandler externalPluginHandler)
		: base("Repair Kits###WorkshoppaRepairKitWindow", "Shop", pluginLog, gameGui, addonLifecycle, externalPluginHandler)
	{
		_pluginLog = pluginLog;
		_configuration = configuration;
	}

	public unsafe override void UpdateShopStock(AtkUnitBase* addon)
	{
		if (GetDarkMatterClusterCount() == 0)
		{
			base.Shop.ItemForSale = null;
			return;
		}
		if (addon->AtkValuesCount != 625)
		{
			_pluginLog.Error($"Unexpected amount of atkvalues for Shop addon ({addon->AtkValuesCount})");
			base.Shop.ItemForSale = null;
			return;
		}
		AtkValue* atkValues = addon->AtkValues;
		if (atkValues->UInt != 0)
		{
			base.Shop.ItemForSale = null;
			return;
		}
		uint itemCount = atkValues[2].UInt;
		if (itemCount == 0)
		{
			base.Shop.ItemForSale = null;
			return;
		}
		base.Shop.ItemForSale = (from i in Enumerable.Range(0, (int)itemCount)
			select new ItemForSale
			{
				Position = i,
				ItemName = atkValues[14 + i].ReadAtkString(),
				Price = atkValues[75 + i].UInt,
				OwnedItems = atkValues[136 + i].UInt,
				ItemId = atkValues[441 + i].UInt
			}).FirstOrDefault((ItemForSale x) => x.ItemId == 10386);
	}

	private int GetDarkMatterClusterCount()
	{
		return base.Shop.GetItemCount(10335u);
	}

	public override int GetCurrencyCount()
	{
		return base.Shop.GetItemCount(1u);
	}

	public override void DrawContent()
	{
		int darkMatterClusters = GetDarkMatterClusterCount();
		if (base.Shop.ItemForSale == null || darkMatterClusters == 0)
		{
			base.IsOpen = false;
			return;
		}
		ImGui.Text("Inventory");
		ImGui.Indent();
		ImU8String text = new ImU8String(22, 1);
		text.AppendLiteral("Dark Matter Clusters: ");
		text.AppendFormatted(darkMatterClusters, "N0");
		ImGui.Text(text);
		ImU8String text2 = new ImU8String(21, 1);
		text2.AppendLiteral("Grade 6 Dark Matter: ");
		text2.AppendFormatted(base.Shop.ItemForSale.OwnedItems, "N0");
		ImGui.Text(text2);
		ImGui.Unindent();
		int missingItems = Math.Max(0, darkMatterClusters * 5 - (int)base.Shop.ItemForSale.OwnedItems);
		Vector4 col = ((missingItems == 0) ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed);
		ImU8String text3 = new ImU8String(29, 1);
		text3.AppendLiteral("Missing Grade 6 Dark Matter: ");
		text3.AppendFormatted(missingItems, "N0");
		ImGui.TextColored(in col, text3);
		if (base.Shop.PurchaseState != null)
		{
			base.Shop.HandleNextPurchaseStep();
			if (base.Shop.PurchaseState != null && ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel Auto-Buy"))
			{
				base.Shop.CancelAutoPurchase();
			}
			return;
		}
		int toPurchase = Math.Min(base.Shop.GetMaxItemsToPurchase(), missingItems);
		if (toPurchase > 0 && ImGuiComponents.IconButtonWithText(FontAwesomeIcon.DollarSign, $"Auto-Buy missing Dark Matter for {base.Shop.ItemForSale.Price * toPurchase:N0}{SeIconChar.Gil.ToIconString()}"))
		{
			base.Shop.StartAutoPurchase(toPurchase);
			base.Shop.HandleNextPurchaseStep();
		}
	}

	public unsafe override void TriggerPurchase(AtkUnitBase* addonShop, int buyNow)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		AtkValue* buyItem = stackalloc AtkValue[4]
		{
			new AtkValue
			{
				Type = (AtkValueType)3,
				Int = 0
			},
			new AtkValue
			{
				Type = (AtkValueType)3,
				Int = base.Shop.ItemForSale.Position
			},
			new AtkValue
			{
				Type = (AtkValueType)3,
				Int = buyNow
			},
			new AtkValue
			{
				Type = (AtkValueType)0,
				Int = 0
			}
		};
		addonShop->FireCallback(4u, buyItem);
	}
}
