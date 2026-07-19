using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Workshoppa.GameData;

internal sealed class WorkshopCache
{
	public IReadOnlyList<WorkshopCraft> Crafts { get; private set; } = new List<WorkshopCraft>();

	public WorkshopCache(IDataManager dataManager, IPluginLog pluginLog)
	{
		WorkshopCache workshopCache = this;
		Task.Run(delegate
		{
			try
			{
				Dictionary<uint, Item> itemMapping = (from x in dataManager.GetExcelSheet<CompanyCraftSupplyItem>()
					where x.RowId != 0
					select x).ToDictionary((CompanyCraftSupplyItem x) => x.RowId, (CompanyCraftSupplyItem x) => x.Item.Value);
				workshopCache.Crafts = (from x in dataManager.GetExcelSheet<CompanyCraftSequence>()
					where x.RowId != 0
					select new WorkshopCraft
					{
						WorkshopItemId = x.RowId,
						ResultItem = x.ResultItem.RowId,
						Name = x.ResultItem.Value.Name.ToString(),
						IconId = x.ResultItem.Value.Icon,
						Category = (WorkshopCraftCategory)x.CompanyCraftDraftCategory.RowId,
						Type = x.CompanyCraftType.RowId,
						Phases = x.CompanyCraftPart.Where((RowRef<CompanyCraftPart> part) => part.RowId != 0).SelectMany((RowRef<CompanyCraftPart> part) => part.Value.CompanyCraftProcess.Select((RowRef<CompanyCraftProcess> y) => new WorkshopCraftPhase
						{
							Name = part.Value.CompanyCraftType.Value.Name.ToString(),
							Items = (from i in Enumerable.Range(0, y.Value.SupplyItem.Count)
								select new
								{
									SupplyItem = y.Value.SupplyItem[i],
									SetsRequired = y.Value.SetsRequired[i],
									SetQuantity = y.Value.SetQuantity[i]
								} into item
								where item.SupplyItem.RowId != 0
								select new WorkshopCraftItem
								{
									ItemId = itemMapping[item.SupplyItem.RowId].RowId,
									Name = itemMapping[item.SupplyItem.RowId].Name.ToString(),
									IconId = itemMapping[item.SupplyItem.RowId].Icon,
									SetQuantity = item.SetQuantity,
									SetsRequired = item.SetsRequired
								}).ToList().AsReadOnly()
						})).ToList()
							.AsReadOnly()
					}).ToList().AsReadOnly();
			}
			catch (Exception exception)
			{
				pluginLog.Error(exception, "Unable to load cached items");
			}
		});
	}
}
