using System;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using SmithingPlus.Util;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace SmithingPlus.CastingTweaks;

[HarmonyPatchCategory(Core.DynamicMoldsCategory)]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[HarmonyPatch(typeof(BlockEntityToolMold))]
public class ToolMoldUnitsPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(BlockEntityToolMold.Initialize))]
    public static void Initialize_Postfix(BlockEntityToolMold __instance, ref int ___requiredUnits, ICoreAPI api)
    {
        // Assume copper stack as metal for unit calculation
        // This means the patch will only apply for standard molds!
        // Either vanilla or vanilla-like ones
        var copperIngot = api.World.GetItem(new AssetLocation("game:ingot-copper"));
        if (copperIngot == null) return;
        var copperStack = new ItemStack(copperIngot);
        var dropStacks = __instance.GetMoldedStacks(copperStack);
        if (dropStacks == null || dropStacks.Length == 0)
            return; // <-- Patch will only apply for molds that work for copper!
        var voxelCount = VoxelCountForStacks(api, dropStacks);
        // These are all assumptions that have to be made, should implement warnings if weird values are found
        const float voxelsPerIngot = 42f;
        const float unitsPerIngot = 100f;
        const float unitsPerVoxel = unitsPerIngot / voxelsPerIngot;
        // Round to lowest 5 units to avoid annoying numbers and making players sad
        var requiredUnitsRounded = (int)MathF.Floor(voxelCount * unitsPerVoxel / 5) * 5;
        ___requiredUnits = requiredUnitsRounded;
    }

    public static int VoxelCountForStacks(ICoreAPI api, ItemStack[] smithedItemStacks)
    {
        var smithingRecipes = smithedItemStacks.Select(stack =>
            VoxelCountForStack(api, stack)).ToArray();
        return smithingRecipes.Sum();
    }

    private static int VoxelCountForStack(ICoreAPI api, ItemStack stack)
    {
        var cheapestRecipe = stack.GetCheapestSmithingRecipe(api);
        if (cheapestRecipe == null) return 0;
        var cheapestOutput = cheapestRecipe.Output.ResolvedItemstack.StackSize;
        var recipeMaterialVoxels = cheapestRecipe.Voxels.VoxelCount();
        var voxelsPerItem = Math.Max(recipeMaterialVoxels / cheapestOutput, 0);
        return voxelsPerItem * stack.StackSize;
    }
}