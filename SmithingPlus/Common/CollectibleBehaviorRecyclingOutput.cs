using System;
using System.Linq;
using SmithingPlus.Util;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace SmithingPlus.Common;

public class CollectibleBehaviorRecyclingOutput(CollectibleObject collObj) : CollectibleBehavior(collObj)
{
    private ICoreAPI Api => collObj.GetField<ICoreAPI>("api");

    // TODO REWRITE TO DETECT CHISEL SLOT!
    // THIS IS CURRENTLY BROKEN
    public override void OnCreatedByCrafting(
        ItemSlot[] allInputslots,
        ItemSlot outputSlot,
        GridRecipe byRecipe, ref EnumHandling bhHandling)
    {
        base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe, ref bhHandling);
        if (outputSlot.Itemstack == null) return;
        var usesChisel = false;
        var chiselStack = allInputslots?.Select(slot => slot.Itemstack)
            ?.FirstOrDefault(stack => stack?.Collectible is ItemChisel);
        if (chiselStack != null)
            usesChisel = byRecipe?.Ingredients?.Values
                .Any(ing => (ing?.SatisfiesAsIngredient(chiselStack) ?? false) && ing.IsTool) ?? false;
        var recipeTools = byRecipe?.Ingredients?.Values.Where(ing =>
                              ing?.RecipeAttributes?[ModRecipeAttributes.RecyclingRecipe]?.AsBool() == true &&
                              ing.IsTool).ToArray() ??
                          [];
        if (!usesChisel && recipeTools.Length == 0) return;
        int? voxelCount = null;

        var inputWorkItemSlot = allInputslots?
            .FirstOrDefault(slot =>
                slot.Itemstack?.Collectible is ItemWorkItem &&
                recipeTools.Any(tool => tool.SatisfiesAsIngredient(slot.Itemstack)));
        if (inputWorkItemSlot != null)
        {
            var voxels = BlockEntityAnvil.deserializeVoxels(inputWorkItemSlot.Itemstack.Attributes.GetBytes("voxels"));
            voxelCount = voxels.MaterialCount();
        }

        // Excludes recipe tools from consideration (don't want to recycle them)
        var inputSmithedItemSlot = allInputslots?
            .FirstOrDefault(slot =>
                slot.Itemstack?.GetLargestSmithingRecipe(Api) != null &&
                !recipeTools.Any(tool => tool.SatisfiesAsIngredient(slot.Itemstack)));
        Core.Logger.Warning("Input smithed item slot: {0}", inputSmithedItemSlot?.Itemstack.Collectible.Code);
        if (voxelCount == null && inputSmithedItemSlot != null)
        {
            var largestRecipe = inputSmithedItemSlot.Itemstack.GetLargestSmithingRecipe(Api);
            if (largestRecipe != null)
            {
                var largestOutput = Math.Max(largestRecipe.Output.ResolvedItemstack.StackSize, 1);
                var totalVoxels = largestRecipe.Voxels.VoxelCount();
                voxelCount = totalVoxels / largestOutput;
            }
        }

        if (voxelCount == null) return;
        outputSlot.Itemstack.StackSize = Math.Max((int)(voxelCount / Core.Config.VoxelsPerBit), 1);
        var sourceSlot = inputWorkItemSlot ?? inputSmithedItemSlot;

        var temperature = sourceSlot.Itemstack.Collectible.GetTemperature(Api.World, sourceSlot.Itemstack);
        outputSlot.Itemstack.Collectible.SetTemperature(Api.World, outputSlot.Itemstack, temperature);
    }
}