using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace LLib;

[Sheet("PleaseSpecifyTheSheetExplicitly")]
public readonly struct QuestDialogueText(ExcelPage page, uint offset, uint row) : IQuestDialogueText, IExcelRow<QuestDialogueText>
{
	public uint RowId => row;

	public ReadOnlySeString Key => page.ReadString(offset, offset);

	public ReadOnlySeString Value => page.ReadString(offset + 4, offset);

	public ExcelPage ExcelPage => page;

	public uint RowOffset => offset;

	static QuestDialogueText IExcelRow<QuestDialogueText>.Create(ExcelPage page, uint offset, uint row)
	{
		return new QuestDialogueText(page, offset, row);
	}
}
