#nullable enable
using System;
using SmithingPlus.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace SmithingPlus.SmithWithBits;

public sealed class CollectibleBehaviorWorkableNugget(CollectibleObject collObj) :
    CollectibleBehaviorAnvilWorkable(collObj)
{
    private static float ExtraVoxelChance => Core.Config.VoxelsPerBit - MathF.Floor(Core.Config.VoxelsPerBit);
    private string[][] Pattern { get; } = [["##", "_o"]];

    protected override byte[,,] Voxels =>
        CollectibleBehaviorJsonAnvilWorkable.GenVoxelsFromJsonPatternWithExtra(Pattern,
            IgnoreExtraVoxels ? null : Api?.World.Rand,
            IgnoreExtraVoxels ? 0 : ExtraVoxelChance);

    private bool IgnoreExtraVoxels { get; set; }

    protected override AnvilPlacementMode PlacementMode
    {
        get
        {
            return Core.Config.SmithWithBits switch
            {
                true when Core.Config.BitsTopUp => AnvilPlacementMode.Normal,
                true when !Core.Config.BitsTopUp => AnvilPlacementMode.Empty,
                false when Core.Config.BitsTopUp => AnvilPlacementMode.Present,
                _ => AnvilPlacementMode.None
            };
        }
    }

    public override int VoxelCountForHandbook(ItemStack stack)
    {
        return 2;
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        IgnoreExtraVoxels = properties[PropertyKeys.IgnoreExtraVoxels].Exists &&
                            properties[PropertyKeys.IgnoreExtraVoxels].AsBool();
    }

    private static class PropertyKeys
    {
        public const string IgnoreExtraVoxels = "ignoreExtraVoxels";
    }
}