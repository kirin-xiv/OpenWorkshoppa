using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace LLib.Shop;

public interface IShopWindow
{
	bool IsEnabled { get; }

	bool IsOpen { get; set; }

	Vector2? Position { get; set; }

	int GetCurrencyCount();

	unsafe void UpdateShopStock(AtkUnitBase* addon);

	unsafe void TriggerPurchase(AtkUnitBase* addonShop, int buyNow);

	void SaveExternalPluginState();

	void RestoreExternalPluginState();
}
