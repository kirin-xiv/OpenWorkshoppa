using System;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace LLib;

public static class DataManagerExtensions
{
	public static ReadOnlySeString? GetSeString<T>(this IDataManager dataManager, string key) where T : struct, IQuestDialogueText, IExcelRow<T>
	{
		ArgumentNullException.ThrowIfNull(dataManager, "dataManager");
		return dataManager.GetExcelSheet<T>().Cast<T?>().SingleOrDefault((T? x) => x.Value.Key == key)?.Value;
	}

	public static string? GetString<T>(this IDataManager dataManager, string key, IPluginLog? pluginLog) where T : struct, IQuestDialogueText, IExcelRow<T>
	{
		string text = dataManager.GetSeString<T>(key)?.WithCertainMacroCodeReplacements();
		pluginLog?.Verbose($"{typeof(T).Name}.{key} => {text}");
		return text;
	}

	public static Regex? GetRegex<T>(this IDataManager dataManager, string key, IPluginLog? pluginLog) where T : struct, IQuestDialogueText, IExcelRow<T>
	{
		ReadOnlySeString? text = dataManager.GetSeString<T>(key);
		if (!text.HasValue)
		{
			return null;
		}
		string regex = string.Join("", text.Select((ReadOnlySePayload payload) => (payload.Type == ReadOnlySePayloadType.Text) ? Regex.Escape(payload.ToString()) : "(.*)"));
		pluginLog?.Verbose($"{typeof(T).Name}.{key} => /{regex}/");
		return new Regex(regex);
	}

	public static ReadOnlySeString? GetSeString<T>(this IDataManager dataManager, uint rowId, Func<T, ReadOnlySeString?> mapper) where T : struct, IExcelRow<T>
	{
		ArgumentNullException.ThrowIfNull(dataManager, "dataManager");
		ArgumentNullException.ThrowIfNull(mapper, "mapper");
		T? row = dataManager.GetExcelSheet<T>().GetRowOrDefault(rowId);
		if (!row.HasValue)
		{
			return null;
		}
		return mapper(row.Value);
	}

	public static string? GetString<T>(this IDataManager dataManager, uint rowId, Func<T, ReadOnlySeString?> mapper, IPluginLog? pluginLog = null) where T : struct, IExcelRow<T>
	{
		string text = dataManager.GetSeString(rowId, mapper)?.WithCertainMacroCodeReplacements();
		pluginLog?.Verbose($"{typeof(T).Name}.{rowId} => {text}");
		return text;
	}

	public static Regex? GetRegex<T>(this IDataManager dataManager, uint rowId, Func<T, ReadOnlySeString?> mapper, IPluginLog? pluginLog = null) where T : struct, IExcelRow<T>
	{
		ReadOnlySeString? text = dataManager.GetSeString(rowId, mapper);
		if (!text.HasValue)
		{
			return null;
		}
		Regex regex = text.ToRegex();
		pluginLog?.Verbose($"{typeof(T).Name}.{rowId} => /{regex}/");
		return regex;
	}

	public static Regex? GetRegex<T>(this T excelRow, Func<T, ReadOnlySeString?> mapper, IPluginLog? pluginLog) where T : struct, IExcelRow<T>
	{
		ArgumentNullException.ThrowIfNull(mapper, "mapper");
		ReadOnlySeString? text = mapper(excelRow);
		if (!text.HasValue)
		{
			return null;
		}
		Regex regex = text.ToRegex();
		pluginLog?.Verbose($"{typeof(T).Name}.regex => /{regex}/");
		return regex;
	}

	public static Regex ToRegex(this ReadOnlySeString? text)
	{
		ArgumentNullException.ThrowIfNull(text, "text");
		return new Regex(string.Join("", text.Value.Select((ReadOnlySePayload payload) => (payload.Type == ReadOnlySePayloadType.Text) ? Regex.Escape(payload.ToString()) : "(.*)")));
	}

	public static string WithCertainMacroCodeReplacements(this ReadOnlySeString text)
	{
		return string.Join("", text.Select((ReadOnlySePayload payload) => payload.Type switch
		{
			ReadOnlySePayloadType.Text => payload.ToString(),
			ReadOnlySePayloadType.Macro => payload.MacroCode switch
			{
				MacroCode.NewLine => "",
				MacroCode.NonBreakingSpace => " ",
				MacroCode.Hyphen => "-",
				MacroCode.SoftHyphen => "",
				_ => payload.ToString(),
			},
			_ => payload.ToString(),
		}));
	}
}
