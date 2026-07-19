using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace LLib.Gear;

public sealed class GearStatsCalculator
{
	private sealed record MateriaInfo(EBaseParam BaseParam, Collection<short> Values, bool HasItem);

	private const uint EternityRingItemId = 8575u;

	private static readonly uint[] CanHaveOffhand = new uint[14]
	{
		2u, 6u, 8u, 12u, 14u, 16u, 18u, 20u, 22u, 24u,
		26u, 28u, 30u, 32u
	};

	private readonly ExcelSheet<Item> _itemSheet;

	private readonly Dictionary<(uint ItemLevel, EBaseParam BaseParam), ushort> _itemLevelStatCaps = new Dictionary<(uint, EBaseParam), ushort>();

	private readonly Dictionary<(EBaseParam BaseParam, int EquipSlotCategory), ushort> _equipSlotCategoryPct;

	private readonly Dictionary<uint, MateriaInfo> _materiaStats;

	public GearStatsCalculator(IDataManager? dataManager)
		: this(dataManager?.GetExcelSheet<ItemLevel>() ?? throw new ArgumentNullException("dataManager"), dataManager.GetExcelSheet<ExtendedBaseParam>(), dataManager.GetExcelSheet<Materia>(), dataManager.GetExcelSheet<Item>())
	{
	}

	public GearStatsCalculator(ExcelSheet<ItemLevel> itemLevelSheet, ExcelSheet<ExtendedBaseParam> baseParamSheet, ExcelSheet<Materia> materiaSheet, ExcelSheet<Item> itemSheet)
	{
		ArgumentNullException.ThrowIfNull(itemLevelSheet, "itemLevelSheet");
		ArgumentNullException.ThrowIfNull(baseParamSheet, "baseParamSheet");
		ArgumentNullException.ThrowIfNull(materiaSheet, "materiaSheet");
		ArgumentNullException.ThrowIfNull(itemSheet, "itemSheet");
		_itemSheet = itemSheet;
		foreach (ItemLevel itemLevel in itemLevelSheet)
		{
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Strength)] = itemLevel.Strength;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Dexterity)] = itemLevel.Dexterity;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Vitality)] = itemLevel.Vitality;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Intelligence)] = itemLevel.Intelligence;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Mind)] = itemLevel.Mind;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Piety)] = itemLevel.Piety;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.GP)] = itemLevel.GP;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.CP)] = itemLevel.CP;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.DamagePhys)] = itemLevel.PhysicalDamage;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.DamageMag)] = itemLevel.MagicalDamage;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.DefensePhys)] = itemLevel.Defense;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.DefenseMag)] = itemLevel.MagicDefense;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Tenacity)] = itemLevel.Tenacity;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Crit)] = itemLevel.CriticalHit;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.DirectHit)] = itemLevel.DirectHitRate;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Determination)] = itemLevel.Determination;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.SpellSpeed)] = itemLevel.SpellSpeed;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.SkillSpeed)] = itemLevel.SkillSpeed;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Gathering)] = itemLevel.Gathering;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Perception)] = itemLevel.Perception;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Craftsmanship)] = itemLevel.Craftsmanship;
			_itemLevelStatCaps[(itemLevel.RowId, EBaseParam.Control)] = itemLevel.Control;
		}
		_equipSlotCategoryPct = baseParamSheet.SelectMany((ExtendedBaseParam x) => from y in Enumerable.Range(0, x.EquipSlotCategoryPct.Count)
			select ((EBaseParam)x.RowId, y: y, x.EquipSlotCategoryPct[y])).ToDictionary(((EBaseParam, int y, ushort) x) => (x.Item1, x.y), ((EBaseParam, int y, ushort) x) => x.Item3);
		_materiaStats = materiaSheet.Where((Materia x) => x.RowId != 0 && x.BaseParam.RowId != 0).ToDictionary((Materia x) => x.RowId, (Materia x) => new MateriaInfo((EBaseParam)x.BaseParam.RowId, x.Value, x.Item[0].RowId != 0));
	}

	public unsafe EquipmentStats CalculateGearStats(InventoryItem* item)
	{
		List<(uint, byte)> materias = new List<(uint, byte)>();
		byte materiaCount = 0;
		if (item->ItemId != 8575)
		{
			for (int i = 0; i < 5; i++)
			{
				ushort materia = item->Materia[i];
				if (materia != 0)
				{
					materiaCount++;
					materias.Add((materia, item->MateriaGrades[i]));
				}
			}
		}
		return CalculateGearStats(_itemSheet.GetRow(item->ItemId), item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality), materias)with
		{
			MateriaCount = materiaCount
		};
	}

	public EquipmentStats CalculateGearStats(Item item, bool highQuality, IReadOnlyList<(uint MateriaId, byte Grade)> materias)
	{
		ArgumentNullException.ThrowIfNull(materias, "materias");
		Dictionary<EBaseParam, StatInfo> result = new Dictionary<EBaseParam, StatInfo>();
		for (int i = 0; i < item.BaseParam.Count; i++)
		{
			AddEquipmentStat(result, item.BaseParam[i], item.BaseParamValue[i]);
		}
		if (highQuality)
		{
			for (int j = 0; j < item.BaseParamSpecial.Count; j++)
			{
				AddEquipmentStat(result, item.BaseParamSpecial[j], item.BaseParamValueSpecial[j]);
			}
		}
		foreach (var materia in materias)
		{
			if (_materiaStats.TryGetValue(materia.MateriaId, out MateriaInfo materiaStat))
			{
				AddMateriaStat(item, result, materiaStat, materia.Grade);
			}
		}
		return new EquipmentStats(result, 0);
	}

	private static void AddEquipmentStat(Dictionary<EBaseParam, StatInfo> result, RowRef<BaseParam> baseParam, short value)
	{
		if (baseParam.RowId != 0)
		{
			if (result.TryGetValue((EBaseParam)baseParam.RowId, out StatInfo statInfo))
			{
				result[(EBaseParam)baseParam.RowId] = statInfo with
				{
					EquipmentValue = (short)(statInfo.EquipmentValue + value)
				};
			}
			else
			{
				result[(EBaseParam)baseParam.RowId] = new StatInfo(value, 0, Overcapped: false);
			}
		}
	}

	private void AddMateriaStat(Item item, Dictionary<EBaseParam, StatInfo> result, MateriaInfo materiaInfo, short grade)
	{
		if (!result.TryGetValue(materiaInfo.BaseParam, out StatInfo statInfo))
		{
			statInfo = (result[materiaInfo.BaseParam] = new StatInfo(0, 0, Overcapped: false));
		}
		if (materiaInfo.HasItem)
		{
			short maximumValue = (short)(GetMaximumStatValue(item, materiaInfo.BaseParam) - statInfo.EquipmentValue);
			if (statInfo.MateriaValue + materiaInfo.Values[grade] > maximumValue)
			{
				result[materiaInfo.BaseParam] = statInfo with
				{
					MateriaValue = maximumValue,
					Overcapped = true
				};
			}
			else
			{
				result[materiaInfo.BaseParam] = statInfo with
				{
					MateriaValue = (short)(statInfo.MateriaValue + materiaInfo.Values[grade])
				};
			}
		}
		else
		{
			result[materiaInfo.BaseParam] = statInfo with
			{
				MateriaValue = (short)(statInfo.MateriaValue + materiaInfo.Values[grade])
			};
		}
	}

	public short GetMaximumStatValue(Item item, EBaseParam baseParamValue)
	{
		if (_itemLevelStatCaps.TryGetValue((item.LevelItem.RowId, baseParamValue), out var stat))
		{
			return (short)Math.Round((float)(stat * _equipSlotCategoryPct[(baseParamValue, (int)item.EquipSlotCategory.RowId)]) / 1000f, MidpointRounding.AwayFromZero);
		}
		return 0;
	}

	public unsafe short CalculateAverageItemLevel(InventoryContainer* container)
	{
		uint sum = 0u;
		int calculatedSlots = 12;
		for (int i = 0; i < 13; i++)
		{
			if (i == 5)
			{
				continue;
			}
			InventoryItem* inventoryItem = container->GetInventorySlot(i);
			if (inventoryItem == null || inventoryItem->ItemId == 0)
			{
				continue;
			}
			Item? item = _itemSheet.GetRowOrDefault(inventoryItem->ItemId);
			if (!item.HasValue)
			{
				continue;
			}
			if (item.Value.ItemUICategory.RowId == 105)
			{
				if (i == 0)
				{
					calculatedSlots--;
				}
				calculatedSlots--;
				continue;
			}
			if (i == 0 && !CanHaveOffhand.Contains(item.Value.ItemUICategory.RowId))
			{
				sum += item.Value.LevelItem.RowId;
				i++;
			}
			sum += item.Value.LevelItem.RowId;
		}
		return (short)(sum / calculatedSlots);
	}
}
