using HarmonyLib;
using SmithingPlus.Util;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SmithingPlus.SkillfulSmithing;

[HarmonyPatch(typeof(BlockEntityAnvil))]
[HarmonyPatchCategory(Core.NeverPatchCategory)]
//[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class SkillfulSmithingPatches
{
    [HarmonyPrefix]
    public static void BlockEntityAnvil_OnUseOver_Prefix(BlockEntityAnvil __instance, out byte __state,
        IPlayer byPlayer, Vec3i voxelPos, BlockSelection blockSel)
    {
        __state = __instance.Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z];
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnUseOver", typeof(IPlayer), typeof(Vec3i), typeof(BlockSelection))]
    public static void BlockEntityAnvil_OnUseOver_Postfix(BlockEntityAnvil __instance, byte __state, IPlayer byPlayer,
        Vec3i voxelPos, BlockSelection blockSel)
    {
        if (byPlayer.Entity.Api.Side.IsClient()) return;
        if (!__instance.CanWorkCurrent) return; // Can't work the item
        var activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeHotbarSlot?.Itemstack == null) return;
        if (__instance.WorkItemStack is not { } workItemStack) return;

        var toolMode = activeHotbarSlot.GetHammerToolMode(byPlayer, blockSel);
        if (toolMode != 0) return; // 5 is the split mode
        var voxelType = __state;
        if (voxelType != 1) // only continue if voxel is metal
            Core.Logger.VerboseDebug("[BitsRecovery] Non-metal voxel type: {0}", voxelType);
    }
}