using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Workshoppa.GameData;

internal sealed class RecipeTree
{
	private sealed class RecipeInfo : Ingredient
	{
		public required uint AmountCrafted { get; init; }

		public required List<uint> DependsOn { get; init; }
	}

	private readonly IDataManager _dataManager;

	private readonly IPluginLog _pluginLog;

	private readonly IReadOnlyList<uint> _shopItemsOnly;

	public RecipeTree(IDataManager dataManager, IPluginLog pluginLog)
	{
		_dataManager = dataManager;
		_pluginLog = pluginLog;
		uint[] shopVendorIds = new uint[8] { 262461u, 262462u, 262463u, 262471u, 262472u, 262692u, 262422u, 262211u };
		_shopItemsOnly = (from x in _dataManager.GetSubrowExcelSheet<GilShopItem>().Flatten()
			where shopVendorIds.Contains(x.RowId)
			select x.Item.RowId into x
			where x != 0
			select x).Distinct().ToList().AsReadOnly();
	}

	public IReadOnlyList<Ingredient> ResolveRecipes(IReadOnlyList<Ingredient> materials)
	{
		int limit = 10;
		List<RecipeInfo> nextStep = ExtendWithAmountCrafted(materials);
		List<RecipeInfo> completeList = new List<RecipeInfo>(nextStep);
		while (--limit > 0 && nextStep.Any((RecipeInfo x) => x.Type == Ingredient.EType.Craftable))
		{
			nextStep = GetIngredients(nextStep);
			completeList.AddRange(nextStep);
		}
		completeList = (from x in completeList
			group x by x.ItemId into x
			select new RecipeInfo
			{
				ItemId = x.Key,
				Name = x.First().Name,
				TotalQuantity = x.Sum((RecipeInfo y) => y.TotalQuantity),
				Type = x.First().Type,
				DependsOn = x.First().DependsOn,
				AmountCrafted = x.First().AmountCrafted
			}).ToList();
		_pluginLog.Verbose("Complete craft list:");
		foreach (RecipeInfo item in completeList)
		{
			_pluginLog.Verbose($"  {item.TotalQuantity}x {item.Name}");
		}
		foreach (RecipeInfo ingredient in completeList.Where((RecipeInfo x) => x != null && x.AmountCrafted > 1))
		{
			foreach (RecipeInfo item4 in completeList.Where((RecipeInfo x) => ingredient.DependsOn.Contains(x.ItemId)))
			{
				int unmodifiedQuantity = item4.TotalQuantity;
				int roundedQuantity = (int)((unmodifiedQuantity + ingredient.AmountCrafted - 1) / ingredient.AmountCrafted);
				item4.TotalQuantity = item4.TotalQuantity - unmodifiedQuantity + roundedQuantity;
			}
		}
		foreach (RecipeInfo item5 in completeList.Where((RecipeInfo x) => x.Type == Ingredient.EType.ShopItem))
		{
			item5.DependsOn.Clear();
		}
		List<RecipeInfo> sortedList = new List<RecipeInfo>();
		while (sortedList.Count < completeList.Count)
		{
			_pluginLog.Verbose("Sort round");
			List<RecipeInfo> canBeCrafted = completeList.Where((RecipeInfo x) => !sortedList.Contains(x) && x.DependsOn.All((uint y) => sortedList.Any((RecipeInfo z) => y == z.ItemId))).ToList();
			foreach (RecipeInfo item2 in canBeCrafted)
			{
				_pluginLog.Verbose($"  can craft: {item2.TotalQuantity}x {item2.Name}");
			}
			if (canBeCrafted.Count == 0)
			{
				foreach (RecipeInfo item3 in completeList.Where((RecipeInfo x) => !sortedList.Contains(x)))
				{
					_pluginLog.Warning($"  can't craft: {item3.TotalQuantity}x {item3.Name} → ({string.Join(", ", item3.DependsOn.Where((uint y) => sortedList.All((RecipeInfo z) => y != z.ItemId)))})");
				}
				throw new InvalidOperationException("Unable to sort items");
			}
			sortedList.AddRange(canBeCrafted.OrderBy((RecipeInfo x) => x.Name));
		}
		return sortedList.Cast<Ingredient>().ToList();
	}

	private List<RecipeInfo> GetIngredients(List<RecipeInfo> materials)
	{
		List<RecipeInfo> ingredients = new List<RecipeInfo>();
		foreach (RecipeInfo material in materials.Where((RecipeInfo x) => x.Type == Ingredient.EType.Craftable))
		{
			Recipe? recipe = GetFirstRecipeForItem(material.ItemId);
			if (!recipe.HasValue)
			{
				continue;
			}
			for (int i = 0; i < 8; i++)
			{
				RowRef<Item> ingredient = recipe.Value.Ingredient[i];
				if (!ingredient.IsValid || ingredient.RowId == 0)
				{
					continue;
				}
				Item item = ingredient.Value;
				if (IsValidItem(item.RowId))
				{
					Recipe? ingredientRecipe = GetFirstRecipeForItem(ingredient.RowId);
					ingredients.Add(new RecipeInfo
					{
						ItemId = ingredient.RowId,
						Name = item.Name.ToString(),
						TotalQuantity = material.TotalQuantity * recipe.Value.AmountIngredient[i],
						Type = (_shopItemsOnly.Contains(ingredient.RowId) ? Ingredient.EType.ShopItem : ((!ingredientRecipe.HasValue) ? (GetGatheringItem(ingredient.RowId).HasValue ? Ingredient.EType.Gatherable : (GetVentureItem(ingredient.RowId).HasValue ? Ingredient.EType.Gatherable : Ingredient.EType.Other)) : Ingredient.EType.Craftable)),
						AmountCrafted = (ingredientRecipe?.AmountResult ?? 1),
						DependsOn = ((ingredientRecipe.HasValue ? (from x in ingredientRecipe.GetValueOrDefault().Ingredient
							where x.IsValid && IsValidItem(x.RowId)
							select x.RowId).ToList() : null) ?? new List<uint>())
					});
				}
			}
		}
		return ingredients;
	}

	private List<RecipeInfo> ExtendWithAmountCrafted(IEnumerable<Ingredient> materials)
	{
		return (from x in materials
			select new
			{
				Ingredient = x,
				Recipe = GetFirstRecipeForItem(x.ItemId)
			} into x
			where x.Recipe.HasValue
			select new RecipeInfo
			{
				ItemId = x.Ingredient.ItemId,
				Name = x.Ingredient.Name,
				TotalQuantity = x.Ingredient.TotalQuantity,
				Type = (_shopItemsOnly.Contains(x.Ingredient.ItemId) ? Ingredient.EType.ShopItem : x.Ingredient.Type),
				AmountCrafted = x.Recipe.Value.AmountResult,
				DependsOn = (from y in x.Recipe.Value.Ingredient
					where y.IsValid && IsValidItem(y.RowId)
					select y.RowId).ToList()
			}).ToList();
	}

	private Recipe? GetFirstRecipeForItem(uint itemId)
	{
		return _dataManager.GetExcelSheet<Recipe>().FirstOrDefault((Recipe x) => x.RowId != 0 && x.ItemResult.RowId == itemId);
	}

	private GatheringItem? GetGatheringItem(uint itemId)
	{
		return _dataManager.GetExcelSheet<GatheringItem>().FirstOrDefault((GatheringItem x) => x.RowId != 0 && x.Item.RowId == itemId);
	}

	private RetainerTaskNormal? GetVentureItem(uint itemId)
	{
		return _dataManager.GetExcelSheet<RetainerTaskNormal>().FirstOrDefault((RetainerTaskNormal x) => x.RowId != 0 && x.Item.RowId == itemId);
	}

	private static bool IsValidItem(uint itemId)
	{
		if (itemId > 19)
		{
			return itemId != uint.MaxValue;
		}
		return false;
	}
}
