using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using LLib.ImGui;
using Workshoppa.GameData;

namespace Workshoppa;

internal sealed class Configuration : IPluginConfiguration
{
	internal sealed class QueuedItem
	{
		public uint WorkshopItemId { get; set; }

		public int Quantity { get; set; }
	}

	internal sealed class CurrentItem
	{
		public uint WorkshopItemId { get; set; }

		public bool StartedCrafting { get; set; }

		public uint PhasesComplete { get; set; }

		public List<PhaseItem> ContributedItemsInCurrentPhase { get; set; } = new List<PhaseItem>();

		public bool UpdateFromCraftState(CraftState craftState)
		{
			bool changed = false;
			if (PhasesComplete != craftState.StepsComplete)
			{
				PhasesComplete = craftState.StepsComplete;
				changed = true;
			}
			if (ContributedItemsInCurrentPhase.Count != craftState.Items.Count)
			{
				ContributedItemsInCurrentPhase = craftState.Items.Select((CraftItem x) => new PhaseItem
				{
					ItemId = x.ItemId,
					QuantityComplete = x.QuantityComplete
				}).ToList();
				changed = true;
			}
			else
			{
				for (int i = 0; i < ContributedItemsInCurrentPhase.Count; i++)
				{
					PhaseItem contributedItem = ContributedItemsInCurrentPhase[i];
					CraftItem craftItem = craftState.Items[i];
					if (contributedItem.ItemId != craftItem.ItemId)
					{
						contributedItem.ItemId = craftItem.ItemId;
						changed = true;
					}
					if (contributedItem.QuantityComplete != craftItem.QuantityComplete)
					{
						contributedItem.QuantityComplete = craftItem.QuantityComplete;
						changed = true;
					}
				}
			}
			return changed;
		}
	}

	internal sealed class PhaseItem
	{
		public uint ItemId { get; set; }

		public uint QuantityComplete { get; set; }
	}

	internal sealed class Preset
	{
		public required Guid Id { get; set; }

		public required string Name { get; set; }

		public List<QueuedItem> ItemQueue { get; set; } = new List<QueuedItem>();
	}

	public int Version { get; set; } = 1;

	public CurrentItem? CurrentlyCraftedItem { get; set; }

	public List<QueuedItem> ItemQueue { get; set; } = new List<QueuedItem>();

	public bool EnableRepairKitCalculator { get; set; } = true;

	public bool EnableCeruleumTankCalculator { get; set; } = true;

	public List<Preset> Presets { get; set; } = new List<Preset>();

	public WindowConfig MainWindowConfig { get; } = new WindowConfig();

	public WindowConfig ConfigWindowConfig { get; } = new WindowConfig();
}
