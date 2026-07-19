using System.Data;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using LLib;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Workshoppa.GameData;

internal sealed class GameStrings
{
	[Sheet("custom/001/CmnDefCompanyManufactory_00150")]
	private readonly struct WorkshopDialogue(ExcelPage page, uint offset, uint row) : IQuestDialogueText, IExcelRow<WorkshopDialogue>
	{
		public uint RowId => row;

		public ReadOnlySeString Key => page.ReadString(offset, offset);

		public ReadOnlySeString Value => page.ReadString(offset + 4, offset);

		public ExcelPage ExcelPage => page;

		public uint RowOffset => offset;

		static WorkshopDialogue IExcelRow<WorkshopDialogue>.Create(ExcelPage page, uint offset, uint row)
		{
			return new WorkshopDialogue(page, offset, row);
		}
	}

	public Regex PurchaseItemForGil { get; }

	public Regex PurchaseItemForCompanyCredits { get; }

	public string ViewCraftingLog { get; }

	public string TurnInHighQualityItem { get; }

	public Regex ContributeItems { get; }

	public Regex RetrieveFinishedItem { get; }

	public GameStrings(IDataManager dataManager, IPluginLog pluginLog)
	{
		PurchaseItemForGil = dataManager.GetRegex(3406u, (Addon addon) => addon.Text, pluginLog) ?? throw new ConstraintException("Unable to resolve PurchaseItemForGil");
		PurchaseItemForCompanyCredits = dataManager.GetRegex(3473u, (Addon addon) => addon.Text, pluginLog) ?? throw new ConstraintException("Unable to resolve PurchaseItemForCompanyCredits");
		ViewCraftingLog = dataManager.GetString<WorkshopDialogue>("TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_NOTE", pluginLog) ?? throw new ConstraintException("Unable to resolve ViewCraftingLog");
		TurnInHighQualityItem = dataManager.GetString(102434u, (Addon addon) => addon.Text, pluginLog) ?? throw new ConstraintException("Unable to resolve TurnInHighQualityItem");
		ContributeItems = dataManager.GetRegex(6652u, (Addon addon) => addon.Text, pluginLog) ?? throw new ConstraintException("Unable to resolve ContributeItems");
		RetrieveFinishedItem = dataManager.GetRegex<WorkshopDialogue>("TEXT_CMNDEFCOMPANYMANUFACTORY_00150_FINISH_CONF", pluginLog) ?? throw new ConstraintException("Unable to resolve RetrieveFinishedItem");
	}
}
