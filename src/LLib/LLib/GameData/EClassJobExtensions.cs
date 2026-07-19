using System;
using System.Linq;

namespace LLib.GameData;

public static class EClassJobExtensions
{
	public static bool IsClass(this EClassJob classJob)
	{
		bool flag;
		switch (classJob)
		{
		case EClassJob.Gladiator:
		case EClassJob.Pugilist:
		case EClassJob.Marauder:
		case EClassJob.Lancer:
		case EClassJob.Archer:
		case EClassJob.Conjurer:
		case EClassJob.Thaumaturge:
		case EClassJob.Arcanist:
		case EClassJob.Rogue:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag && !classJob.IsCrafter())
		{
			return classJob.IsGatherer();
		}
		return true;
	}

	public static bool HasBaseClass(this EClassJob classJob)
	{
		return (from x in Enum.GetValues<EClassJob>()
			where x.IsClass()
			select x).Any((EClassJob x) => x.AsJob() == classJob);
	}

	public static EClassJob AsJob(this EClassJob classJob)
	{
		return classJob switch
		{
			EClassJob.Gladiator => EClassJob.Paladin,
			EClassJob.Marauder => EClassJob.Warrior,
			EClassJob.Pugilist => EClassJob.Monk,
			EClassJob.Lancer => EClassJob.Dragoon,
			EClassJob.Rogue => EClassJob.Ninja,
			EClassJob.Archer => EClassJob.Bard,
			EClassJob.Conjurer => EClassJob.WhiteMage,
			EClassJob.Thaumaturge => EClassJob.BlackMage,
			EClassJob.Arcanist => EClassJob.Summoner,
			_ => classJob,
		};
	}

	public static bool IsTank(this EClassJob classJob)
	{
		switch (classJob)
		{
		case EClassJob.Gladiator:
		case EClassJob.Marauder:
		case EClassJob.Paladin:
		case EClassJob.Warrior:
		case EClassJob.DarkKnight:
		case EClassJob.Gunbreaker:
			return true;
		default:
			return false;
		}
	}

	public static bool IsHealer(this EClassJob classJob)
	{
		switch (classJob)
		{
		case EClassJob.Conjurer:
		case EClassJob.WhiteMage:
		case EClassJob.Scholar:
		case EClassJob.Astrologian:
		case EClassJob.Sage:
			return true;
		default:
			return false;
		}
	}

	public static bool IsMelee(this EClassJob classJob)
	{
		switch (classJob)
		{
		case EClassJob.Pugilist:
		case EClassJob.Lancer:
		case EClassJob.Monk:
		case EClassJob.Dragoon:
		case EClassJob.Rogue:
		case EClassJob.Ninja:
		case EClassJob.Samurai:
		case EClassJob.Reaper:
		case EClassJob.Viper:
			return true;
		default:
			return false;
		}
	}

	public static bool IsPhysicalRanged(this EClassJob classJob)
	{
		switch (classJob)
		{
		case EClassJob.Archer:
		case EClassJob.Bard:
		case EClassJob.Machinist:
		case EClassJob.Dancer:
			return true;
		default:
			return false;
		}
	}

	public static bool IsCaster(this EClassJob classJob)
	{
		switch (classJob)
		{
		case EClassJob.Thaumaturge:
		case EClassJob.BlackMage:
		case EClassJob.Arcanist:
		case EClassJob.Summoner:
		case EClassJob.RedMage:
		case EClassJob.BlueMage:
		case EClassJob.Pictomancer:
			return true;
		default:
			return false;
		}
	}

	public static bool DealsPhysicalDamage(this EClassJob classJob)
	{
		if (!classJob.IsTank() && !classJob.IsMelee())
		{
			return classJob.IsPhysicalRanged();
		}
		return true;
	}

	public static bool DealsMagicDamage(this EClassJob classJob)
	{
		if (!classJob.IsHealer())
		{
			return classJob.IsCaster();
		}
		return true;
	}

	public static bool IsCrafter(this EClassJob classJob)
	{
		if (classJob >= EClassJob.Carpenter)
		{
			return classJob <= EClassJob.Culinarian;
		}
		return false;
	}

	public static bool IsGatherer(this EClassJob classJob)
	{
		if (classJob >= EClassJob.Miner)
		{
			return classJob <= EClassJob.Fisher;
		}
		return false;
	}

	public static string ToFriendlyString(this EClassJob classJob)
	{
		return classJob switch
		{
			EClassJob.WhiteMage => "White Mage",
			EClassJob.BlackMage => "Black Mage",
			EClassJob.DarkKnight => "Dark Knight",
			EClassJob.RedMage => "Red Mage",
			EClassJob.BlueMage => "Blue Mage",
			_ => classJob.ToString(),
		};
	}
}
