using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using LLib.ImGui;
using Workshoppa.GameData;

namespace Workshoppa.Windows;

internal sealed class MainWindow : LWindow, IPersistableWindowConfig
{
	public enum ButtonState
	{
		None,
		Start,
		Resume,
		Pause,
		Stop
	}

	public enum EOpenReason
	{
		None,
		Command,
		NearFabricationStation,
		PluginInstaller
	}

	private static readonly Regex CountAndName = new Regex("^(\\d{1,5})x?\\s+(.*)$", RegexOptions.Compiled);

	private readonly WorkshopPlugin _plugin;

	private readonly IDalamudPluginInterface _pluginInterface;

	private readonly IClientState _clientState;

	private readonly IObjectTable _objectTable;

	private readonly Configuration _configuration;

	private readonly WorkshopCache _workshopCache;

	private readonly IconCache _iconCache;

	private readonly IChatGui _chatGui;

	private readonly RecipeTree _recipeTree;

	private readonly IPluginLog _pluginLog;

	private string _searchString = string.Empty;

	private bool _checkInventory;

	private string _newPresetName = string.Empty;

	public EOpenReason OpenReason { get; set; }

	public bool NearFabricationStation { get; set; }

	public ButtonState State { get; set; }

	private bool IsDiscipleOfHand
	{
		get
		{
			if (_objectTable.LocalPlayer != null)
			{
				uint rowId = _objectTable.LocalPlayer.ClassJob.RowId;
				if (rowId >= 8)
				{
					return rowId <= 15;
				}
				return false;
			}
			return false;
		}
	}

	public WindowConfig WindowConfig => _configuration.MainWindowConfig;

	public MainWindow(WorkshopPlugin plugin, IDalamudPluginInterface pluginInterface, IClientState clientState, IObjectTable objectTable, Configuration configuration, WorkshopCache workshopCache, IconCache iconCache, IChatGui chatGui, RecipeTree recipeTree, IPluginLog pluginLog)
		: base("Workshoppa###WorkshoppaMainWindow")
	{
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		_plugin = plugin;
		_pluginInterface = pluginInterface;
		_clientState = clientState;
		_objectTable = objectTable;
		_configuration = configuration;
		_workshopCache = workshopCache;
		_iconCache = iconCache;
		_chatGui = chatGui;
		_recipeTree = recipeTree;
		_pluginLog = pluginLog;
		base.Position = new Vector2(100f, 100f);
		base.PositionCondition = ImGuiCond.FirstUseEver;
		base.SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(350f, 50f),
			MaximumSize = new Vector2(500f, 9999f)
		};
		base.Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.MenuBar;
		base.AllowClickthrough = false;
	}

	public override void DrawContent()
	{
		if (ImGui.BeginMenuBar())
		{
			ImGui.BeginDisabled(_plugin.CurrentStage != Stage.Stopped);
			DrawPresetsMenu();
			DrawClipboardMenu();
			ImGui.EndDisabled();
			ImGui.EndMenuBar();
		}
		Configuration.CurrentItem currentItem = _configuration.CurrentlyCraftedItem;
		if (currentItem != null)
		{
			WorkshopCraft currentCraft = _workshopCache.Crafts.Single((WorkshopCraft x) => x.WorkshopItemId == currentItem.WorkshopItemId);
			ImU8String text = new ImU8String(19, 0);
			text.AppendLiteral("Currently Crafting:");
			ImGui.Text(text);
			IDalamudTextureWrap icon = _iconCache.GetIcon(currentCraft.IconId);
			if (icon != null)
			{
				ImGui.Image(icon.Handle, new Vector2(ImGui.GetFrameHeight()));
				ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) / 2f);
			}
			ImU8String text2 = new ImU8String(0, 1);
			text2.AppendFormatted(currentCraft.Name);
			ImGui.TextUnformatted(text2);
			ImGui.Spacing();
			if (_plugin.CurrentStage == Stage.Stopped)
			{
				if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Search, "Check Inventory"))
				{
					_checkInventory = !_checkInventory;
				}
				ImGui.SameLine();
				ImGui.BeginDisabled(!NearFabricationStation || !IsDiscipleOfHand);
				if (currentItem.StartedCrafting)
				{
					if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Resume"))
					{
						State = ButtonState.Resume;
						_checkInventory = false;
					}
				}
				else if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start Crafting"))
				{
					State = ButtonState.Start;
					_checkInventory = false;
				}
				ImGui.EndDisabled();
				ImGui.SameLine();
				bool keysHeld = ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift;
				ImGui.BeginDisabled(!keysHeld);
				if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel"))
				{
					State = ButtonState.Pause;
					_configuration.CurrentlyCraftedItem = null;
					Save();
				}
				ImGui.EndDisabled();
				if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !keysHeld)
				{
					ImU8String tooltip = new ImU8String(171, 0);
					tooltip.AppendLiteral("Hold CTRL+SHIFT to remove this as craft. You have to manually use the fabrication station to cancel or finish the workshop project before you can continue using the queue.");
					ImGui.SetTooltip(tooltip);
				}
				ShowErrorConditions();
			}
			else
			{
				ImGui.BeginDisabled(_plugin.CurrentStage == Stage.RequestStop);
				if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Pause, "Pause"))
				{
					State = ButtonState.Pause;
				}
				ImGui.EndDisabled();
			}
		}
		else
		{
			ImGui.Text("Currently Crafting: ---");
			if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Search, "Check Inventory"))
			{
				_checkInventory = !_checkInventory;
			}
			ImGui.SameLine();
			ImGui.BeginDisabled(!NearFabricationStation || _configuration.ItemQueue.Sum((Configuration.QueuedItem x) => x.Quantity) == 0 || _plugin.CurrentStage != Stage.Stopped || !IsDiscipleOfHand);
			if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start Crafting"))
			{
				State = ButtonState.Start;
				_checkInventory = false;
			}
			ImGui.EndDisabled();
			ShowErrorConditions();
		}
		if (_checkInventory)
		{
			ImGui.Separator();
			CheckMaterial();
		}
		ImGui.Separator();
		ImGui.Text("Queue:");
		ImGui.BeginDisabled(_plugin.CurrentStage != Stage.Stopped);
		Configuration.QueuedItem itemToRemove = null;
		for (int i = 0; i < _configuration.ItemQueue.Count; i++)
		{
			ImU8String imU8String = new ImU8String(9, 1);
			imU8String.AppendLiteral("ItemQueue");
			imU8String.AppendFormatted(i);
			using (ImRaii.PushId(imU8String, true))
			{
				Configuration.QueuedItem item = _configuration.ItemQueue[i];
				WorkshopCraft craft = _workshopCache.Crafts.Single((WorkshopCraft x) => x.WorkshopItemId == item.WorkshopItemId);
				IDalamudTextureWrap icon2 = _iconCache.GetIcon(craft.IconId);
				if (icon2 != null)
				{
					ImGui.Image(icon2.Handle, new Vector2(ImGui.GetFrameHeight()));
					ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
				}
				ImGui.SetNextItemWidth(Math.Max(100f * ImGui.GetIO().FontGlobalScale, 4f * (ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.X)));
				int quantity = item.Quantity;
				if (ImGui.InputInt(craft.Name, ref quantity))
				{
					item.Quantity = Math.Max(0, quantity);
					Save();
				}
				ImU8String strId = new ImU8String(10, 1);
				strId.AppendLiteral("###Context");
				strId.AppendFormatted(i);
				ImGui.OpenPopupOnItemClick(strId);
				ImU8String imU8String2 = new ImU8String(10, 1);
				imU8String2.AppendLiteral("###Context");
				imU8String2.AppendFormatted(i);
				using (var popup = ImRaii.ContextPopup(imU8String2))
				{
					if (popup)
					{
						ImU8String label = new ImU8String(7, 1);
						label.AppendLiteral("Remove ");
						label.AppendFormatted(craft.Name);
						if (ImGui.MenuItem(label))
						{
							itemToRemove = item;
						}
					}
				}
			}
		}
		if (itemToRemove != null)
		{
			_configuration.ItemQueue.Remove(itemToRemove);
			Save();
		}
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		if (ImGui.BeginCombo("##CraftSelection", "Add Craft...", ImGuiComboFlags.HeightLarge))
		{
			ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
			ImGui.InputTextWithHint("", "Filter...", ref _searchString, 256);
			foreach (WorkshopCraft craft2 in from x in _workshopCache.Crafts
				where x.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase)
				orderby x.WorkshopItemId
				select x)
			{
				IDalamudTextureWrap icon3 = _iconCache.GetIcon(craft2.IconId);
				Vector2 pos = ImGui.GetCursorPos();
				Vector2 iconSize = new Vector2(ImGui.GetTextLineHeight() + ImGui.GetStyle().ItemSpacing.Y);
				if (icon3 != null)
				{
					ImGui.SetCursorPos(pos + new Vector2(iconSize.X + ImGui.GetStyle().FramePadding.X, ImGui.GetStyle().ItemSpacing.Y / 2f));
				}
				ImU8String label2 = new ImU8String(13, 2);
				label2.AppendFormatted(craft2.Name);
				label2.AppendLiteral("##SelectCraft");
				label2.AppendFormatted(craft2.WorkshopItemId);
				if (ImGui.Selectable(label2, selected: false, ImGuiSelectableFlags.SpanAllColumns))
				{
					_configuration.ItemQueue.Add(new Configuration.QueuedItem
					{
						WorkshopItemId = craft2.WorkshopItemId,
						Quantity = 1
					});
					Save();
				}
				if (icon3 != null)
				{
					ImGui.SameLine(0f, 0f);
					ImGui.SetCursorPos(pos);
					ImGui.Image(icon3.Handle, iconSize);
				}
			}
			ImGui.EndCombo();
		}
		ImGui.EndDisabled();
		ImGui.Separator();
		ImU8String text3 = new ImU8String(15, 1);
		text3.AppendLiteral("Debug (Stage): ");
		text3.AppendFormatted(_plugin.CurrentStage);
		ImGui.Text(text3);
	}

	private void DrawPresetsMenu()
	{
		if (!ImGui.BeginMenu("Presets"))
		{
			return;
		}
		if (_configuration.Presets.Count == 0)
		{
			ImGui.BeginDisabled();
			ImGui.MenuItem("Import Queue from Preset");
			ImGui.EndDisabled();
		}
		else if (ImGui.BeginMenu("Import Queue from Preset"))
		{
			if (_configuration.Presets.Count == 0)
			{
				ImGui.MenuItem("You have no presets.");
			}
			foreach (Configuration.Preset preset in _configuration.Presets)
			{
				ImU8String strId = new ImU8String(6, 1);
				strId.AppendLiteral("Preset");
				strId.AppendFormatted(preset.Id);
				ImGui.PushID(strId);
				if (ImGui.MenuItem(preset.Name))
				{
					foreach (Configuration.QueuedItem item in preset.ItemQueue)
					{
						Configuration.QueuedItem queuedItem = _configuration.ItemQueue.FirstOrDefault((Configuration.QueuedItem x) => x.WorkshopItemId == item.WorkshopItemId);
						if (queuedItem != null)
						{
							queuedItem.Quantity += item.Quantity;
							continue;
						}
						_configuration.ItemQueue.Add(new Configuration.QueuedItem
						{
							WorkshopItemId = item.WorkshopItemId,
							Quantity = item.Quantity
						});
					}
					Save();
					_chatGui.Print($"Imported {preset.ItemQueue.Count} items from preset.");
				}
				ImGui.PopID();
			}
			ImGui.EndMenu();
		}
		if (_configuration.ItemQueue.Count == 0)
		{
			ImGui.BeginDisabled();
			ImGui.MenuItem("Export Queue to Preset");
			ImGui.EndDisabled();
		}
		else if (ImGui.BeginMenu("Export Queue to Preset"))
		{
			ImGui.InputTextWithHint("", "Preset Name...", ref _newPresetName, 64);
			ImGui.BeginDisabled(_configuration.Presets.Any((Configuration.Preset x) => x.Name.Equals(_newPresetName, StringComparison.OrdinalIgnoreCase)));
			if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, "Save"))
			{
				_configuration.Presets.Add(new Configuration.Preset
				{
					Id = Guid.NewGuid(),
					Name = _newPresetName,
					ItemQueue = _configuration.ItemQueue.Select((Configuration.QueuedItem x) => new Configuration.QueuedItem
					{
						WorkshopItemId = x.WorkshopItemId,
						Quantity = x.Quantity
					}).ToList()
				});
				Save();
				_chatGui.Print("Saved queue as preset '" + _newPresetName + "'.");
				_newPresetName = string.Empty;
			}
			ImGui.EndDisabled();
			ImGui.EndMenu();
		}
		if (_configuration.Presets.Count == 0)
		{
			ImGui.BeginDisabled();
			ImGui.MenuItem("Delete Preset");
			ImGui.EndDisabled();
		}
		else if (ImGui.BeginMenu("Delete Preset"))
		{
			if (_configuration.Presets.Count == 0)
			{
				ImGui.MenuItem("You have no presets.");
			}
			Guid? presetToRemove = null;
			foreach (Configuration.Preset preset2 in _configuration.Presets)
			{
				ImU8String strId2 = new ImU8String(6, 1);
				strId2.AppendLiteral("Preset");
				strId2.AppendFormatted(preset2.Id);
				ImGui.PushID(strId2);
				if (ImGui.MenuItem(preset2.Name))
				{
					presetToRemove = preset2.Id;
				}
				ImGui.PopID();
			}
			if (presetToRemove.HasValue)
			{
				Configuration.Preset preset3 = _configuration.Presets.First(delegate(Configuration.Preset x)
				{
					Guid id = x.Id;
					Guid? guid = presetToRemove;
					return id == guid;
				});
				_configuration.Presets.Remove(preset3);
				Save();
				_chatGui.Print("Deleted preset '" + preset3.Name + "'.");
			}
			ImGui.EndMenu();
		}
		ImGui.EndMenu();
	}

	private void DrawClipboardMenu()
	{
		if (!ImGui.BeginMenu("Clipboard"))
		{
			return;
		}
		List<Configuration.QueuedItem> fromClipboardItems = new List<Configuration.QueuedItem>();
		try
		{
			string clipboardText = GetClipboardText();
			if (!string.IsNullOrWhiteSpace(clipboardText))
			{
				string[] array = clipboardText.ReplaceLineEndings().Split(Environment.NewLine);
				foreach (string clipboardLine in array)
				{
					Match match = CountAndName.Match(clipboardLine);
					if (match.Success)
					{
						WorkshopCraft craft = _workshopCache.Crafts.FirstOrDefault((WorkshopCraft x) => x.Name.Equals(match.Groups[2].Value, StringComparison.OrdinalIgnoreCase));
						if (craft != null && int.TryParse(match.Groups[1].Value, out var quantity))
						{
							fromClipboardItems.Add(new Configuration.QueuedItem
							{
								WorkshopItemId = craft.WorkshopItemId,
								Quantity = quantity
							});
						}
					}
				}
			}
		}
		catch (Exception)
		{
		}
		ImGui.BeginDisabled(fromClipboardItems.Count == 0);
		if (ImGui.MenuItem("Import Queue from Clipboard"))
		{
			_pluginLog.Information($"Importing {fromClipboardItems.Count} items...");
			int count = 0;
			foreach (Configuration.QueuedItem item in fromClipboardItems)
			{
				Configuration.QueuedItem queuedItem = _configuration.ItemQueue.FirstOrDefault((Configuration.QueuedItem x) => x.WorkshopItemId == item.WorkshopItemId);
				if (queuedItem != null)
				{
					queuedItem.Quantity += item.Quantity;
				}
				else
				{
					_configuration.ItemQueue.Add(new Configuration.QueuedItem
					{
						WorkshopItemId = item.WorkshopItemId,
						Quantity = item.Quantity
					});
				}
				count++;
			}
			Save();
			_chatGui.Print($"Imported {count} items from clipboard.");
		}
		ImGui.EndDisabled();
		ImGui.BeginDisabled(_configuration.ItemQueue.Count == 0);
		if (ImGui.MenuItem("Export Queue to Clipboard"))
		{
			IEnumerable<string> toClipboardItems = from x in _configuration.ItemQueue
				select new
				{
					_workshopCache.Crafts.Single((WorkshopCraft y) => x.WorkshopItemId == y.WorkshopItemId).Name,
					x.Quantity
				} into x
				select $"{x.Quantity}x {x.Name}";
			ImGui.SetClipboardText(string.Join(Environment.NewLine, toClipboardItems));
			_chatGui.Print("Copied queue content to clipboard.");
		}
		if (ImGui.MenuItem("Export Material List to Clipboard"))
		{
			IEnumerable<Ingredient> toClipboardItems2 = from x in _recipeTree.ResolveRecipes(GetMaterialList())
				where x.Type == Ingredient.EType.Craftable
				select x;
			ImGui.SetClipboardText(string.Join(Environment.NewLine, toClipboardItems2.Select((Ingredient x) => $"{x.TotalQuantity}x {x.Name}")));
			_chatGui.Print("Copied material list to clipboard.");
		}
		if (ImGui.MenuItem("Export Gathered/Venture materials to Clipboard"))
		{
			IEnumerable<Ingredient> toClipboardItems3 = from x in _recipeTree.ResolveRecipes(GetMaterialList())
				where x.Type == Ingredient.EType.Gatherable
				select x;
			ImGui.SetClipboardText(string.Join(Environment.NewLine, toClipboardItems3.Select((Ingredient x) => $"{x.TotalQuantity}x {x.Name}")));
			_chatGui.Print("Copied material list to clipboard.");
		}
		ImGui.EndDisabled();
		ImGui.EndMenu();
	}

	private unsafe string? GetClipboardText()
	{
		byte* ptr = ImGuiNative.GetClipboardText();
		if (ptr == null)
		{
			return null;
		}
		int byteCount;
		for (byteCount = 0; ptr[byteCount] != 0; byteCount++)
		{
		}
		return Encoding.UTF8.GetString(ptr, byteCount);
	}

	private void Save()
	{
		_pluginInterface.SavePluginConfig(_configuration);
	}

	public void Toggle(EOpenReason reason)
	{
		if (!base.IsOpen)
		{
			base.IsOpen = true;
			OpenReason = reason;
		}
		else
		{
			base.IsOpen = false;
		}
	}

	public override void OnClose()
	{
		OpenReason = EOpenReason.None;
	}

	private unsafe void CheckMaterial()
	{
		ImGui.Text("Items needed for all crafts in queue:");
		List<Ingredient> materialList = GetMaterialList();
		ImGui.Indent(20f);
		InventoryManager* inventoryManager = InventoryManager.Instance();
		foreach (Ingredient item in materialList)
		{
			int inInventory = inventoryManager->GetInventoryItemCount(item.ItemId, isHq: true, checkEquipped: false, checkArmory: false, 0) + inventoryManager->GetInventoryItemCount(item.ItemId, isHq: false, checkEquipped: false, checkArmory: false, 0);
			IDalamudTextureWrap icon = _iconCache.GetIcon(item.IconId);
			if (icon != null)
			{
				ImGui.Image(icon.Handle, new Vector2(ImGui.GetFrameHeight()));
				ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
				ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) / 2f);
				icon.Dispose();
			}
			Vector4 col = ((inInventory >= item.TotalQuantity) ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed);
			ImU8String text = new ImU8String(6, 3);
			text.AppendFormatted(item.Name);
			text.AppendLiteral(" (");
			text.AppendFormatted(inInventory);
			text.AppendLiteral(" / ");
			text.AppendFormatted(item.TotalQuantity);
			text.AppendLiteral(")");
			ImGui.TextColored(in col, text);
		}
		ImGui.Unindent(20f);
	}

	private List<Ingredient> GetMaterialList()
	{
		List<uint> workshopItemIds = _configuration.ItemQueue.SelectMany((Configuration.QueuedItem x) => from _ in Enumerable.Range(0, x.Quantity)
			select x.WorkshopItemId).ToList();
		Dictionary<uint, int> completedForCurrentCraft = new Dictionary<uint, int>();
		Configuration.CurrentItem currentItem = _configuration.CurrentlyCraftedItem;
		if (currentItem != null)
		{
			workshopItemIds.Add(currentItem.WorkshopItemId);
			WorkshopCraft craft = _workshopCache.Crafts.Single((WorkshopCraft x) => x.WorkshopItemId == currentItem.WorkshopItemId);
			for (int i = 0; i < currentItem.PhasesComplete; i++)
			{
				foreach (WorkshopCraftItem item in craft.Phases[i].Items)
				{
					AddMaterial(completedForCurrentCraft, item.ItemId, item.TotalQuantity);
				}
			}
			if (currentItem.PhasesComplete < craft.Phases.Count)
			{
				foreach (Configuration.PhaseItem item2 in currentItem.ContributedItemsInCurrentPhase)
				{
					AddMaterial(completedForCurrentCraft, item2.ItemId, (int)item2.QuantityComplete);
				}
			}
		}
		return (from x in workshopItemIds.Select((uint x) => _workshopCache.Crafts.Single((WorkshopCraft y) => y.WorkshopItemId == x)).SelectMany((WorkshopCraft x) => x.Phases).SelectMany((WorkshopCraftPhase x) => x.Items)
			group x by new { x.Name, x.ItemId, x.IconId } into x
			orderby x.Key.Name
			select new Ingredient
			{
				ItemId = x.Key.ItemId,
				IconId = x.Key.IconId,
				Name = x.Key.Name,
				TotalQuantity = (completedForCurrentCraft.TryGetValue(x.Key.ItemId, out var value) ? (x.Sum((WorkshopCraftItem y) => y.TotalQuantity) - value) : x.Sum((WorkshopCraftItem y) => y.TotalQuantity)),
				Type = Ingredient.EType.Craftable
			}).ToList();
	}

	private static void AddMaterial(Dictionary<uint, int> completedForCurrentCraft, uint itemId, int quantity)
	{
		if (completedForCurrentCraft.TryGetValue(itemId, out var existingQuantity))
		{
			completedForCurrentCraft[itemId] = quantity + existingQuantity;
		}
		else
		{
			completedForCurrentCraft[itemId] = quantity;
		}
	}

	private void ShowErrorConditions()
	{
		if (!_plugin.WorkshopTerritories.Contains(_clientState.TerritoryType))
		{
			ImGui.TextColored(ImGuiColors.DalamudRed, "You are not in the Company Workshop.");
		}
		else if (!NearFabricationStation)
		{
			ImGui.TextColored(ImGuiColors.DalamudRed, "You are not near a Fabrication Station.");
		}
		if (!IsDiscipleOfHand)
		{
			ImGui.TextColored(ImGuiColors.DalamudRed, "You need to be a Disciple of the Hand to start crafting.");
		}
	}

	public void SaveWindowConfig()
	{
		Save();
	}
}
