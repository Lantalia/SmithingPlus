#nullable enable
using Vintagestory.API.Common;

namespace SmithingPlus.Util;

public static class ItemExtensions
{
    public static Item? ItemWithVariant(this Item item, string type, string value)
    {
        var api = item.GetField<ICoreAPI>("api");
        if (api != null) return api.World.GetItem(item.CodeWithVariant(type, value));
        Core.Logger.Error("Reflection failed to get collectible object api field");
        return null;
    }
}