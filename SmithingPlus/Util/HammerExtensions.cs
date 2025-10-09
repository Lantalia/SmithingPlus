using Vintagestory.API.Common;

namespace SmithingPlus.Util;

internal static class HammerExtensions
{
    public enum HammerToolMode
    {
        HeavyHit = 0,
        UpsetNorth = 1,
        UpsetEast = 2,
        UpsetSouth = 3,
        UpsetWest = 4,
        Split = 5
    }

    public static HammerToolMode GetHammerToolMode(this ItemSlot hotbarSlot, IPlayer byPlayer, BlockSelection blockSel)
    {
        return (HammerToolMode)hotbarSlot.Itemstack.Collectible.GetToolMode(hotbarSlot, byPlayer, blockSel);
    }
}