using ECommons.ExcelServices.Sheets;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace LLib.Gear;

[Sheet("BaseParam")]
public readonly struct ExtendedBaseParam(ExcelPage page, uint offset, uint row) : IRowExtension<ExtendedBaseParam, BaseParam>, IExcelRow<ExtendedBaseParam>
{
	private const int ParamCount = 23;

	public BaseParam BaseParam => new BaseParam(page, offset, row);

	public unsafe Collection<ushort> EquipSlotCategoryPct => new Collection<ushort>(page, offset, offset, (delegate*<ExcelPage, uint, uint, uint, ushort>)(&EquipSlotCategoryPctCtor), 23);

	public uint RowId => row;

	public ExcelPage ExcelPage => page;

	public uint RowOffset => offset;

	private static ushort EquipSlotCategoryPctCtor(ExcelPage page, uint parentOffset, uint offset, uint i)
	{
		if (i != 0)
		{
			return page.ReadUInt16(offset + 8 + (i - 1) * 2);
		}
		return 0;
	}

	public static ExtendedBaseParam Create(ExcelPage page, uint offset, uint row)
	{
		return new ExtendedBaseParam(page, offset, row);
	}
}
