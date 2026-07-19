using System;
using System.Collections.Generic;
using System.Linq;

namespace LLib.Gear;

public sealed record EquipmentStats(Dictionary<EBaseParam, StatInfo> Stats, byte MateriaCount)
{
	private sealed class KeyValuePairComparer : IEqualityComparer<KeyValuePair<EBaseParam, StatInfo>>
	{
		public bool Equals(KeyValuePair<EBaseParam, StatInfo> x, KeyValuePair<EBaseParam, StatInfo> y)
		{
			if (x.Key == y.Key)
			{
				return object.Equals(x.Value, y.Value);
			}
			return false;
		}

		public int GetHashCode(KeyValuePair<EBaseParam, StatInfo> obj)
		{
			return HashCode.Combine((int)obj.Key, obj.Value);
		}
	}

	public short Get(EBaseParam param)
	{
		return (short)(GetEquipment(param) + GetMateria(param));
	}

	public short GetEquipment(EBaseParam param)
	{
		Stats.TryGetValue(param, out StatInfo v);
		return v?.EquipmentValue ?? 0;
	}

	public short GetMateria(EBaseParam param)
	{
		Stats.TryGetValue(param, out StatInfo v);
		return v?.MateriaValue ?? 0;
	}

	public bool IsOvercapped(EBaseParam param)
	{
		Stats.TryGetValue(param, out StatInfo v);
		return v?.Overcapped ?? false;
	}

	public bool Has(EBaseParam substat)
	{
		return Stats.ContainsKey(substat);
	}

	public bool HasMateria()
	{
		return Stats.Values.Any((StatInfo x) => x.MateriaValue > 0);
	}

	public bool Equals(EquipmentStats? other)
	{
		if (other != null && MateriaCount == other.MateriaCount)
		{
			return Stats.SequenceEqual(other.Stats, new KeyValuePairComparer());
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(MateriaCount, Stats);
	}
}
