using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;
using LLib.GameUI;
using Workshoppa.External;
using Workshoppa.GameData;
using Workshoppa.Windows;

namespace Workshoppa;

public sealed class WorkshopPlugin : IDalamudPlugin, IDisposable
{
	private uint? _contributingItemId;

	private readonly IReadOnlyList<uint> _fabricationStationIds = new uint[5] { 2005236u, 2005238u, 2005240u, 2007821u, 2011588u }.AsReadOnly();

	internal readonly IReadOnlyList<uint> WorkshopTerritories = new uint[5] { 423u, 424u, 425u, 653u, 984u }.AsReadOnly();

	private readonly WindowSystem _windowSystem = new WindowSystem("WorkshopPlugin");

	private readonly IDalamudPluginInterface _pluginInterface;

	private readonly IGameGui _gameGui;

	private readonly IFramework _framework;

	private readonly ICondition _condition;

	private readonly IClientState _clientState;

	private readonly IObjectTable _objectTable;

	private readonly ICommandManager _commandManager;

	private readonly IPluginLog _pluginLog;

	private readonly IAddonLifecycle _addonLifecycle;

	private readonly IChatGui _chatGui;

	private readonly Configuration _configuration;

	private readonly ExternalPluginHandler _externalPluginHandler;

	private readonly WorkshopCache _workshopCache;

	private readonly GameStrings _gameStrings;

	private readonly MainWindow _mainWindow;

	private readonly ConfigWindow _configWindow;

	private readonly RepairKitWindow _repairKitWindow;

	private readonly CeruleumTankWindow _ceruleumTankWindow;

	private Stage _currentStageInternal = Stage.Stopped;

	private DateTime _continueAt = DateTime.MinValue;

	private DateTime _fallbackAt = DateTime.MaxValue;

	internal Stage CurrentStage
	{
		get
		{
			return _currentStageInternal;
		}
		private set
		{
			if (_currentStageInternal != value)
			{
				_pluginLog.Debug($"Changing stage from {_currentStageInternal} to {value}");
				_currentStageInternal = value;
			}
			if (value != Stage.Stopped)
			{
				_mainWindow.Flags |= ImGuiWindowFlags.NoCollapse;
			}
			else
			{
				_mainWindow.Flags &= ~ImGuiWindowFlags.NoCollapse;
			}
		}
	}

	private unsafe bool CheckContinueWithDelivery()
	{
		if (_configuration.CurrentlyCraftedItem != null)
		{
			AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
			if (addonMaterialDelivery == null)
			{
				return false;
			}
			_pluginLog.Warning("Material delivery window is open, although unexpected... checking current craft");
			CraftState craftState = ReadCraftState(addonMaterialDelivery);
			if (craftState == null || craftState.ResultItem == 0)
			{
				_pluginLog.Error("Unable to read craft state");
				_continueAt = DateTime.Now.AddSeconds(1.0);
				return false;
			}
			WorkshopCraft craft = _workshopCache.Crafts.SingleOrDefault((WorkshopCraft x) => x.ResultItem == craftState.ResultItem);
			if (craft == null || craft.WorkshopItemId != _configuration.CurrentlyCraftedItem.WorkshopItemId)
			{
				_pluginLog.Error("Unable to match currently crafted item with game state");
				_continueAt = DateTime.Now.AddSeconds(1.0);
				return false;
			}
			_pluginLog.Information("Delivering materials for current active craft, switching to delivery");
			return true;
		}
		return false;
	}

	private void SelectCraftBranch()
	{
		if (SelectSelectString("contrib", 0, (string s) => s.StartsWith("Contribute materials.", StringComparison.Ordinal)))
		{
			CurrentStage = Stage.ContributeMaterials;
			_continueAt = DateTime.Now.AddSeconds(1.0);
		}
		else if (SelectSelectString("advance", 0, (string s) => s.StartsWith("Advance to the next phase of production.", StringComparison.Ordinal)))
		{
			_pluginLog.Information("Phase is complete");
			_configuration.CurrentlyCraftedItem.PhasesComplete++;
			_configuration.CurrentlyCraftedItem.ContributedItemsInCurrentPhase = new List<Configuration.PhaseItem>();
			_pluginInterface.SavePluginConfig(_configuration);
			CurrentStage = Stage.TargetFabricationStation;
			_continueAt = DateTime.Now.AddSeconds(3.0);
		}
		else if (SelectSelectString("complete", 0, (string s) => s.StartsWith("Complete the construction of", StringComparison.Ordinal)))
		{
			_pluginLog.Information("Item is almost complete, confirming last cutscene");
			CurrentStage = Stage.TargetFabricationStation;
			_continueAt = DateTime.Now.AddSeconds(3.0);
		}
		else if (SelectSelectString("collect", 0, (string s) => s == "Collect finished product."))
		{
			_pluginLog.Information("Item is complete");
			CurrentStage = Stage.ConfirmCollectProduct;
			_continueAt = DateTime.Now.AddSeconds(0.25);
		}
	}

	private unsafe void ContributeMaterials()
	{
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
		if (addonMaterialDelivery == null)
		{
			return;
		}
		CraftState craftState = ReadCraftState(addonMaterialDelivery);
		if (craftState == null || craftState.ResultItem == 0)
		{
			_pluginLog.Warning("Could not parse craft state");
			_continueAt = DateTime.Now.AddSeconds(1.0);
			return;
		}
		if (_configuration.CurrentlyCraftedItem.UpdateFromCraftState(craftState))
		{
			_pluginLog.Information("Saving updated current craft information");
			_pluginInterface.SavePluginConfig(_configuration);
		}
		for (int i = 0; i < craftState.Items.Count; i++)
		{
			CraftItem item = craftState.Items[i];
			if (item.Finished)
			{
				continue;
			}
			if (!HasItemInSingleSlot(item.ItemId, item.ItemCountPerStep))
			{
				_pluginLog.Error($"Can't contribute item {item.ItemId} to craft, couldn't find {item.ItemCountPerStep}x in a single inventory slot");
				InventoryManager* inventoryManager = InventoryManager.Instance();
				int itemCount = 0;
				if (inventoryManager != null)
				{
					itemCount = inventoryManager->GetInventoryItemCount(item.ItemId, isHq: true, checkEquipped: false, checkArmory: false, 0) + inventoryManager->GetInventoryItemCount(item.ItemId, isHq: false, checkEquipped: false, checkArmory: false, 0);
				}
				if (itemCount < item.ItemCountPerStep)
				{
					_chatGui.PrintError($"[Workshoppa] You don't have the needed {item.ItemCountPerStep}x {item.ItemName} to continue.");
				}
				else
				{
					_chatGui.PrintError($"[Workshoppa] You don't have {item.ItemCountPerStep}x {item.ItemName} in a single stack, you need to merge the items in your inventory manually to continue.");
				}
				CurrentStage = Stage.RequestStop;
			}
			else
			{
				_externalPluginHandler.SaveTextAdvance();
				_pluginLog.Information($"Contributing {item.ItemCountPerStep}x {item.ItemName}");
				_contributingItemId = item.ItemId;
				AtkValue* contributeMaterial = stackalloc AtkValue[4]
				{
					new AtkValue
					{
						Type = (AtkValueType)3,
						Int = 0
					},
					new AtkValue
					{
						Type = (AtkValueType)5,
						Int = i
					},
					new AtkValue
					{
						Type = (AtkValueType)5,
						UInt = item.ItemCountPerStep
					},
					new AtkValue
					{
						Type = (AtkValueType)0,
						Int = 0
					}
				};
				addonMaterialDelivery->FireCallback(4u, contributeMaterial);
				_fallbackAt = DateTime.Now.AddSeconds(0.2);
				CurrentStage = Stage.OpenRequestItemWindow;
			}
			break;
		}
	}

	private unsafe void RequestPostSetup(AddonEvent type, AddonArgs addon)
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		AddonRequest* addonRequest = (AddonRequest*)addon.Addon.Address;
		_pluginLog.Verbose($"{"RequestPostSetup"}: {CurrentStage}, {addonRequest->EntryCount}");
		if (CurrentStage == Stage.OpenRequestItemWindow && addonRequest->EntryCount == 1)
		{
			_fallbackAt = DateTime.MaxValue;
			CurrentStage = Stage.OpenRequestItemSelect;
			AtkValue* contributeMaterial = stackalloc AtkValue[4]
			{
				new AtkValue
				{
					Type = (AtkValueType)3,
					Int = 2
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					UInt = 44u
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					UInt = 0u
				}
			};
			addonRequest->AtkUnitBase.FireCallback(4u, contributeMaterial);
		}
	}

	private unsafe void ContextIconMenuPostReceiveEvent(AddonEvent type, AddonArgs addon)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		if (CurrentStage == Stage.OpenRequestItemSelect)
		{
			CurrentStage = Stage.ConfirmRequestItemWindow;
			AtkValue* selectSlot = stackalloc AtkValue[5]
			{
				new AtkValue
				{
					Type = (AtkValueType)3,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)3,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					UInt = 20802u
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					UInt = 0u
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				}
			};
			((AddonContextIconMenu*)addon.Addon.Address)->AtkUnitBase.FireCallback(5u, selectSlot);
		}
	}

	private unsafe void RequestPostRefresh(AddonEvent type, AddonArgs addon)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		_pluginLog.Verbose($"{"RequestPostRefresh"}: {CurrentStage}");
		if (CurrentStage == Stage.ConfirmRequestItemWindow)
		{
			AddonRequest* addonRequest = (AddonRequest*)addon.Addon.Address;
			if (addonRequest->EntryCount == 1)
			{
				CurrentStage = Stage.ConfirmMaterialDelivery;
				AtkValue* closeWindow = stackalloc AtkValue[4]
				{
					new AtkValue
					{
						Type = (AtkValueType)3,
						Int = 0
					},
					new AtkValue
					{
						Type = (AtkValueType)5,
						UInt = 0u
					},
					new AtkValue
					{
						Type = (AtkValueType)5,
						UInt = 0u
					},
					new AtkValue
					{
						Type = (AtkValueType)5,
						UInt = 0u
					}
				};
				addonRequest->AtkUnitBase.FireCallback(4u, closeWindow);
				addonRequest->AtkUnitBase.Close(fireCallback: false);
				_externalPluginHandler.RestoreTextAdvance();
			}
		}
	}

	private unsafe void ConfirmMaterialDeliveryFollowUp()
	{
		AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
		if (addonMaterialDelivery == null)
		{
			return;
		}
		CraftState craftState = ReadCraftState(addonMaterialDelivery);
		if (craftState == null || craftState.ResultItem == 0)
		{
			_pluginLog.Warning("Could not parse craft state");
			_continueAt = DateTime.Now.AddSeconds(1.0);
			return;
		}
		CraftItem item = craftState.Items.Single((CraftItem x) => x.ItemId == _contributingItemId);
		item.StepsComplete++;
		if (craftState.IsPhaseComplete())
		{
			CurrentStage = Stage.TargetFabricationStation;
			_continueAt = DateTime.Now.AddSeconds(0.5);
			return;
		}
		_configuration.CurrentlyCraftedItem.ContributedItemsInCurrentPhase.Single((Configuration.PhaseItem x) => x.ItemId == item.ItemId).QuantityComplete = item.QuantityComplete;
		_pluginInterface.SavePluginConfig(_configuration);
		CurrentStage = Stage.ContributeMaterials;
		_continueAt = DateTime.Now.AddSeconds(1.0);
	}

	private void InteractWithFabricationStation(IGameObject fabricationStation)
	{
		InteractWithTarget(fabricationStation);
	}

	private void TakeItemFromQueue()
	{
		if (_configuration.CurrentlyCraftedItem == null)
		{
			while (_configuration.ItemQueue.Count > 0 && _configuration.CurrentlyCraftedItem == null)
			{
				Configuration.QueuedItem firstItem = _configuration.ItemQueue[0];
				if (firstItem.Quantity > 0)
				{
					_configuration.CurrentlyCraftedItem = new Configuration.CurrentItem
					{
						WorkshopItemId = firstItem.WorkshopItemId
					};
					if (firstItem.Quantity > 1)
					{
						firstItem.Quantity--;
					}
					else
					{
						_configuration.ItemQueue.Remove(firstItem);
					}
				}
				else
				{
					_configuration.ItemQueue.Remove(firstItem);
				}
			}
			_pluginInterface.SavePluginConfig(_configuration);
			if (_configuration.CurrentlyCraftedItem != null)
			{
				CurrentStage = Stage.TargetFabricationStation;
			}
			else
			{
				CurrentStage = Stage.RequestStop;
			}
		}
		else
		{
			CurrentStage = Stage.TargetFabricationStation;
		}
	}

	private void OpenCraftingLog()
	{
		if (SelectSelectString("craftlog", 0, (string s) => s == _gameStrings.ViewCraftingLog))
		{
			CurrentStage = Stage.SelectCraftCategory;
		}
	}

	private unsafe void SelectCraftCategory()
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
		if (addonCraftingLog != null)
		{
			WorkshopCraft craft = GetCurrentCraft();
			_pluginLog.Information($"Selecting category {craft.Category} and type {craft.Type}");
			AtkValue* selectCategory = stackalloc AtkValue[8]
			{
				new AtkValue
				{
					Type = (AtkValueType)3,
					Int = 2
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					UInt = (uint)craft.Category
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					UInt = craft.Type
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				}
			};
			addonCraftingLog->FireCallback(8u, selectCategory);
			CurrentStage = Stage.SelectCraft;
			_continueAt = DateTime.Now.AddSeconds(0.1);
		}
	}

	private unsafe void SelectCraft()
	{
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
		if (addonCraftingLog != null)
		{
			WorkshopCraft craft = GetCurrentCraft();
			AtkValue* atkValues = addonCraftingLog->AtkValues;
			uint shownItemCount = atkValues[13].UInt;
			if ((from i in Enumerable.Range(0, (int)shownItemCount)
				select new
				{
					WorkshopItemId = atkValues[14 + 4 * i].UInt,
					Name = atkValues[17 + 4 * i].ReadAtkString()
				}).ToList().All(x => x.WorkshopItemId != craft.WorkshopItemId))
			{
				_pluginLog.Error("Could not find " + craft.Name + " in current list, is it unlocked?");
				CurrentStage = Stage.RequestStop;
				return;
			}
			_pluginLog.Information($"Selecting craft {craft.WorkshopItemId}");
			AtkValue* selectCraft = stackalloc AtkValue[8]
			{
				new AtkValue
				{
					Type = (AtkValueType)3,
					Int = 1
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)5,
					UInt = craft.WorkshopItemId
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				},
				new AtkValue
				{
					Type = (AtkValueType)0,
					Int = 0
				}
			};
			addonCraftingLog->FireCallback(8u, selectCraft);
			CurrentStage = Stage.ConfirmCraft;
			_continueAt = DateTime.Now.AddSeconds(0.1);
		}
	}

	private void ConfirmCraft()
	{
		if (SelectSelectYesno(0, (string s) => s.StartsWith("Craft ", StringComparison.Ordinal)))
		{
			_configuration.CurrentlyCraftedItem.StartedCrafting = true;
			_pluginInterface.SavePluginConfig(_configuration);
			CurrentStage = Stage.TargetFabricationStation;
		}
	}

	public WorkshopPlugin(IDalamudPluginInterface pluginInterface, IGameGui gameGui, IFramework framework, ICondition condition, IClientState clientState, IObjectTable objectTable, IDataManager dataManager, ICommandManager commandManager, IPluginLog pluginLog, IAddonLifecycle addonLifecycle, IChatGui chatGui, ITextureProvider textureProvider)
	{
		_pluginInterface = pluginInterface;
		_gameGui = gameGui;
		_framework = framework;
		_condition = condition;
		_clientState = clientState;
		_objectTable = objectTable;
		_commandManager = commandManager;
		_pluginLog = pluginLog;
		_addonLifecycle = addonLifecycle;
		_chatGui = chatGui;
		_externalPluginHandler = new ExternalPluginHandler(_pluginInterface, _pluginLog);
		_configuration = ((Configuration)_pluginInterface.GetPluginConfig()) ?? new Configuration();
		_workshopCache = new WorkshopCache(dataManager, _pluginLog);
		_gameStrings = new GameStrings(dataManager, _pluginLog);
		_mainWindow = new MainWindow(this, _pluginInterface, _clientState, _objectTable, _configuration, _workshopCache, new IconCache(textureProvider), _chatGui, new RecipeTree(dataManager, _pluginLog), _pluginLog);
		_windowSystem.AddWindow((Window)_mainWindow);
		_configWindow = new ConfigWindow(_pluginInterface, _configuration);
		_windowSystem.AddWindow((Window)_configWindow);
		_repairKitWindow = new RepairKitWindow(_pluginLog, _gameGui, addonLifecycle, _configuration, _externalPluginHandler);
		_windowSystem.AddWindow((Window)_repairKitWindow);
		_ceruleumTankWindow = new CeruleumTankWindow(_pluginLog, _gameGui, addonLifecycle, _configuration, _externalPluginHandler, _chatGui);
		_windowSystem.AddWindow((Window)_ceruleumTankWindow);
		_pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
		_pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
		_pluginInterface.UiBuilder.OpenConfigUi += _configWindow.Toggle;
		_framework.Update += FrameworkUpdate;
		_commandManager.AddHandler("/ws", new CommandInfo(ProcessCommand)
		{
			HelpMessage = "Open UI"
		});
		_commandManager.AddHandler("/workshoppa", new CommandInfo(ProcessCommand)
		{
			ShowInHelp = false
		});
		_commandManager.AddHandler("/buy-tanks", new CommandInfo(ProcessBuyCommand)
		{
			HelpMessage = "Buy a given number of ceruleum tank stacks."
		});
		_commandManager.AddHandler("/fill-tanks", new CommandInfo(ProcessFillCommand)
		{
			HelpMessage = "Fill your inventory with a given number of ceruleum tank stacks."
		});
		_addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);
		_addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
		_addonLifecycle.RegisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
		_addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
	}

	private void FrameworkUpdate(IFramework framework)
	{
		if (!_clientState.IsLoggedIn || !WorkshopTerritories.Contains(_clientState.TerritoryType) || _condition[ConditionFlag.BoundByDuty] || _condition[ConditionFlag.BetweenAreas] || _condition[ConditionFlag.BetweenAreas51] || GetDistanceToEventObject(_fabricationStationIds, out IGameObject fabricationStation) >= 3f)
		{
			_mainWindow.NearFabricationStation = false;
			if (_mainWindow.IsOpen && _mainWindow.OpenReason == MainWindow.EOpenReason.NearFabricationStation && _configuration.CurrentlyCraftedItem == null && _configuration.ItemQueue.Count == 0)
			{
				_mainWindow.IsOpen = false;
			}
		}
		else
		{
			if (!(DateTime.Now >= _continueAt))
			{
				return;
			}
			_mainWindow.NearFabricationStation = true;
			if (!_mainWindow.IsOpen)
			{
				_mainWindow.IsOpen = true;
				_mainWindow.OpenReason = MainWindow.EOpenReason.NearFabricationStation;
			}
			MainWindow.ButtonState state = _mainWindow.State;
			if ((uint)(state - 3) <= 1u)
			{
				_mainWindow.State = MainWindow.ButtonState.None;
				if (CurrentStage != Stage.Stopped)
				{
					_externalPluginHandler.Restore();
					CurrentStage = Stage.Stopped;
				}
				return;
			}
			state = _mainWindow.State;
			bool flag = (uint)(state - 1) <= 1u;
			if (flag && CurrentStage == Stage.Stopped)
			{
				_mainWindow.State = MainWindow.ButtonState.None;
				CurrentStage = Stage.TakeItemFromQueue;
			}
			if (CurrentStage != Stage.Stopped && CurrentStage != Stage.RequestStop && !_externalPluginHandler.Saved)
			{
				_externalPluginHandler.Save();
			}
			switch (CurrentStage)
			{
			case Stage.TakeItemFromQueue:
				if (CheckContinueWithDelivery())
				{
					CurrentStage = Stage.ContributeMaterials;
				}
				else
				{
					TakeItemFromQueue();
				}
				break;
			case Stage.TargetFabricationStation:
			{
				Configuration.CurrentItem currentlyCraftedItem = _configuration.CurrentlyCraftedItem;
				if (currentlyCraftedItem != null && currentlyCraftedItem.StartedCrafting)
				{
					CurrentStage = Stage.SelectCraftBranch;
				}
				else
				{
					CurrentStage = Stage.OpenCraftingLog;
				}
				InteractWithFabricationStation(fabricationStation);
				break;
			}
			case Stage.OpenCraftingLog:
				OpenCraftingLog();
				break;
			case Stage.SelectCraftCategory:
				SelectCraftCategory();
				break;
			case Stage.SelectCraft:
				SelectCraft();
				break;
			case Stage.ConfirmCraft:
				ConfirmCraft();
				break;
			case Stage.RequestStop:
				_externalPluginHandler.Restore();
				CurrentStage = Stage.Stopped;
				break;
			case Stage.SelectCraftBranch:
				SelectCraftBranch();
				break;
			case Stage.ContributeMaterials:
				ContributeMaterials();
				break;
			case Stage.OpenRequestItemWindow:
				if (DateTime.Now > _fallbackAt)
				{
					goto case Stage.ContributeMaterials;
				}
				break;
			default:
				_pluginLog.Warning($"Unknown stage {CurrentStage}");
				break;
			case Stage.OpenRequestItemSelect:
			case Stage.ConfirmRequestItemWindow:
			case Stage.ConfirmMaterialDelivery:
			case Stage.ConfirmCollectProduct:
			case Stage.Stopped:
				break;
			}
		}
	}

	private WorkshopCraft GetCurrentCraft()
	{
		return _workshopCache.Crafts.Single((WorkshopCraft x) => x.WorkshopItemId == _configuration.CurrentlyCraftedItem.WorkshopItemId);
	}

	private void ProcessCommand(string command, string arguments)
	{
		if ((arguments == "c" || arguments == "config") ? true : false)
		{
			_configWindow.Toggle();
		}
		else
		{
			_mainWindow.Toggle(MainWindow.EOpenReason.Command);
		}
	}

	private void ProcessBuyCommand(string command, string arguments)
	{
		if (_ceruleumTankWindow.TryParseBuyRequest(arguments, out var missingQuantity))
		{
			_ceruleumTankWindow.StartPurchase(missingQuantity);
		}
		else
		{
			_chatGui.PrintError("Usage: " + command + " <stacks>");
		}
	}

	private void ProcessFillCommand(string command, string arguments)
	{
		if (_ceruleumTankWindow.TryParseFillRequest(arguments, out var missingQuantity))
		{
			_ceruleumTankWindow.StartPurchase(missingQuantity);
		}
		else
		{
			_chatGui.PrintError("Usage: " + command + " <stacks>");
		}
	}

	private void OpenMainUi()
	{
		_mainWindow.Toggle(MainWindow.EOpenReason.PluginInstaller);
	}

	public void Dispose()
	{
		_addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
		_addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
		_addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
		_addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);
		_commandManager.RemoveHandler("/fill-tanks");
		_commandManager.RemoveHandler("/buy-tanks");
		_commandManager.RemoveHandler("/workshoppa");
		_commandManager.RemoveHandler("/ws");
		_pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
		_pluginInterface.UiBuilder.OpenConfigUi -= _configWindow.Toggle;
		_pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
		_framework.Update -= FrameworkUpdate;
		_ceruleumTankWindow.Dispose();
		_repairKitWindow.Dispose();
		_externalPluginHandler.RestoreTextAdvance();
		_externalPluginHandler.Restore();
	}

	private unsafe void InteractWithTarget(IGameObject obj)
	{
		_pluginLog.Information($"Setting target to {obj}");
		TargetSystem.Instance()->InteractWithObject((GameObject*)obj.Address, checkLineOfSight: false);
	}

	private float GetDistanceToEventObject(IReadOnlyList<uint> npcIds, out IGameObject? o)
	{
		Vector3? localPlayerPosition = _objectTable.LocalPlayer?.Position;
		if (localPlayerPosition.HasValue)
		{
			foreach (IGameObject obj in _objectTable)
			{
				if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj && npcIds.Contains(obj.BaseId))
				{
					o = obj;
					float distance = Vector3.Distance(localPlayerPosition.Value, obj.Position + new Vector3(0f, -2f, 0f));
					if ((double)distance > 0.01)
					{
						return distance;
					}
				}
			}
		}
		o = null;
		return float.MaxValue;
	}

	private unsafe AtkUnitBase* GetCompanyCraftingLogAddon()
	{
		if (_gameGui.TryGetAddonByName<AtkUnitBase>("CompanyCraftRecipeNoteBook", out var addon) && LAddon.IsAddonReady(addon))
		{
			return addon;
		}
		return null;
	}

	private unsafe AtkUnitBase* GetMaterialDeliveryAddon()
	{
		AgentInterface* agentInterface = AgentModule.Instance()->GetAgentByInternalId(AgentId.CompanyCraftMaterial);
		if (agentInterface != null && agentInterface->IsAgentActive())
		{
			uint addonId = agentInterface->GetAddonId();
			if (addonId == 0)
			{
				return null;
			}
			AtkUnitBase* addon = LAddon.GetAddonById(addonId);
			if (LAddon.IsAddonReady(addon))
			{
				return addon;
			}
		}
		return null;
	}

	private unsafe bool SelectSelectString(string marker, int choice, Predicate<string> predicate)
	{
		if (_gameGui.TryGetAddonByName<AddonSelectString>("SelectString", out var addonSelectString) && LAddon.IsAddonReady(&addonSelectString->AtkUnitBase))
		{
			if (addonSelectString->PopupMenu.PopupMenu.EntryCount < choice)
			{
				return false;
			}
			CStringPointer textPointer = addonSelectString->PopupMenu.PopupMenu.EntryNames[choice];
			if (!textPointer.HasValue)
			{
				return false;
			}
			string text = MemoryHelper.ReadSeStringNullTerminated(new IntPtr((byte*)textPointer)).ToString();
			_pluginLog.Verbose($"SelectSelectString for {marker}, Choice would be '{text}'");
			if (predicate(text))
			{
				addonSelectString->AtkUnitBase.FireCallbackInt(choice);
				return true;
			}
		}
		return false;
	}

	private unsafe bool SelectSelectYesno(int choice, Predicate<string> predicate)
	{
		if (_gameGui.TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addonSelectYesno) && LAddon.IsAddonReady(&addonSelectYesno->AtkUnitBase))
		{
			string text = MemoryHelper.ReadSeString(&addonSelectYesno->PromptText->NodeText).ToString();
			text = text.Replace("\n", "", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal);
			if (predicate(text))
			{
				_pluginLog.Information($"Selecting choice {choice} for '{text}'");
				addonSelectYesno->AtkUnitBase.FireCallbackInt(choice);
				return true;
			}
			_pluginLog.Verbose("Text " + text + " does not match");
		}
		return false;
	}

	private unsafe CraftState? ReadCraftState(AtkUnitBase* addonMaterialDelivery)
	{
		try
		{
			AtkValue* atkValues = addonMaterialDelivery->AtkValues;
			if (addonMaterialDelivery->AtkValuesCount == 157 && atkValues != null)
			{
				uint resultItem = atkValues->UInt;
				uint stepsComplete = atkValues[6].UInt;
				uint stepsTotal = atkValues[7].UInt;
				uint listItemCount = atkValues[11].UInt;
				List<CraftItem> items = (from i in Enumerable.Range(0, (int)listItemCount)
					select new CraftItem
					{
						ItemId = atkValues[12 + i].UInt,
						IconId = atkValues[24 + i].UInt,
						ItemName = atkValues[36 + i].ReadAtkString(),
						CrafterIconId = atkValues[48 + i].Int,
						ItemCountPerStep = atkValues[60 + i].UInt,
						ItemCountNQ = atkValues[72 + i].UInt,
						ItemCountHQ = ParseAtkItemCountHq(atkValues[84 + i]),
						Experience = atkValues[96 + i].UInt,
						StepsComplete = atkValues[108 + i].UInt,
						StepsTotal = atkValues[120 + i].UInt,
						Finished = (atkValues[132 + i].UInt != 0),
						CrafterMinimumLevel = atkValues[144 + i].UInt
					}).ToList();
				return new CraftState
				{
					ResultItem = resultItem,
					StepsComplete = stepsComplete,
					StepsTotal = stepsTotal,
					Items = items
				};
			}
		}
		catch (Exception exception)
		{
			_pluginLog.Warning(exception, "Could not parse CompanyCraftMaterial info");
		}
		return null;
	}

	private static uint ParseAtkItemCountHq(AtkValue atkValue)
	{
		string s = atkValue.ReadAtkString();
		if (s != null)
		{
			string[] parts = s.Replace("\ue03c", "", StringComparison.Ordinal).Split('/');
			if (parts.Length > 1)
			{
				return uint.Parse(parts[1].Replace(",", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal).Trim(), CultureInfo.InvariantCulture);
			}
		}
		return 0u;
	}

	private unsafe bool HasItemInSingleSlot(uint itemId, uint count)
	{
		InventoryManager* inventoryManger = InventoryManager.Instance();
		if (inventoryManger == null)
		{
			return false;
		}
		for (InventoryType t = InventoryType.Inventory1; t <= InventoryType.Inventory4; t++)
		{
			InventoryContainer* container = inventoryManger->GetInventoryContainer(t);
			for (int i = 0; i < container->Size; i++)
			{
				InventoryItem* item = container->GetInventorySlot(i);
				if (item != null && item->ItemId == itemId && item->Quantity >= count)
				{
					return true;
				}
			}
		}
		return false;
	}

	private unsafe void SelectYesNoPostSetup(AddonEvent type, AddonArgs args)
	{
		_pluginLog.Verbose("SelectYesNo post-setup");
		AddonSelectYesno* addonSelectYesNo = (AddonSelectYesno*)args.Addon.Address;
		string text = MemoryHelper.ReadSeString(&addonSelectYesNo->PromptText->NodeText).ToString().Replace("\n", "", StringComparison.Ordinal)
			.Replace("\r", "", StringComparison.Ordinal);
		_pluginLog.Verbose("YesNo prompt: '" + text + "'");
		if (_repairKitWindow.IsOpen)
		{
			_pluginLog.Verbose($"Checking for Repair Kit YesNo ({_repairKitWindow.AutoBuyEnabled}, {_repairKitWindow.IsAwaitingYesNo})");
			if (_repairKitWindow.AutoBuyEnabled && _repairKitWindow.IsAwaitingYesNo && _gameStrings.PurchaseItemForGil.IsMatch(text))
			{
				_pluginLog.Information("Selecting 'yes' (" + text + ")");
				_repairKitWindow.IsAwaitingYesNo = false;
				addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
			}
			else
			{
				_pluginLog.Verbose("Not a purchase confirmation match");
			}
		}
		else if (_ceruleumTankWindow.IsOpen)
		{
			_pluginLog.Verbose($"Checking for Ceruleum Tank YesNo ({_ceruleumTankWindow.AutoBuyEnabled}, {_ceruleumTankWindow.IsAwaitingYesNo})");
			if (_ceruleumTankWindow.AutoBuyEnabled && _ceruleumTankWindow.IsAwaitingYesNo && _gameStrings.PurchaseItemForCompanyCredits.IsMatch(text))
			{
				_pluginLog.Information("Selecting 'yes' (" + text + ")");
				_ceruleumTankWindow.IsAwaitingYesNo = false;
				addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
			}
			else
			{
				_pluginLog.Verbose("Not a purchase confirmation match");
			}
		}
		else if (CurrentStage != Stage.Stopped)
		{
			if (CurrentStage == Stage.ConfirmMaterialDelivery && _gameStrings.TurnInHighQualityItem == text)
			{
				_pluginLog.Information("Selecting 'yes' (" + text + ")");
				addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
			}
			else if (CurrentStage == Stage.ConfirmMaterialDelivery && _gameStrings.ContributeItems.IsMatch(text))
			{
				_pluginLog.Information("Selecting 'yes' (" + text + ")");
				addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
				ConfirmMaterialDeliveryFollowUp();
			}
			else if (CurrentStage == Stage.ConfirmCollectProduct && _gameStrings.RetrieveFinishedItem.IsMatch(text))
			{
				_pluginLog.Information("Selecting 'yes' (" + text + ")");
				addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
				ConfirmCollectProductFollowUp();
			}
		}
	}

	private void ConfirmCollectProductFollowUp()
	{
		_configuration.CurrentlyCraftedItem = null;
		_pluginInterface.SavePluginConfig(_configuration);
		CurrentStage = Stage.TakeItemFromQueue;
		_continueAt = DateTime.Now.AddSeconds(0.5);
	}
}
