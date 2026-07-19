using System.Collections.Generic;
using System.Linq;

namespace Workshoppa.GameData;

public sealed class CraftState
{
	public required uint ResultItem { get; init; }

	public required uint StepsComplete { get; init; }

	public required uint StepsTotal { get; init; }

	public required IReadOnlyList<CraftItem> Items { get; init; }

	public bool IsPhaseComplete()
	{
		return Items.All((CraftItem x) => x.Finished || x.StepsComplete == x.StepsTotal);
	}

	public bool IsCraftComplete()
	{
		if (StepsComplete == StepsTotal - 1)
		{
			return IsPhaseComplete();
		}
		return false;
	}
}
