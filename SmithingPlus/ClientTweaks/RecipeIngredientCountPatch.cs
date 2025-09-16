#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using SmithingPlus.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SmithingPlus.ClientTweaks;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[HarmonyPatchCategory(Core.ClientTweaksCategories.AnvilShowRecipeVoxels)]
public static class RecipeIngredientCountPatch
{
    private static List<SmithingRecipe> _selectedRecipes = [];
    private static ItemStack? _selectedIngredient;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlockEntityAnvil), "OpenDialog")]
    public static void OpenDialog_Postfx(BlockEntityAnvil __instance, ItemStack? ingredient)
    {
        if (__instance?.Api is not ICoreClientAPI) return;
        if (ingredient != null) _selectedIngredient = ingredient;
        var recipes = ingredient?.Collectible.GetCollectibleInterface<IAnvilWorkable>()?.GetMatchingRecipes(ingredient);
        if (recipes != null) _selectedRecipes = recipes;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialogBlockEntityRecipeSelector), "SetupDialog")]
    public static bool SetupDialog_Prefix(
        GuiDialogBlockEntityRecipeSelector __instance,
        List<SkillItem> ___skillItems,
        int ___prevSlotOver,
        ICoreClientAPI ___capi,
        BlockPos ___blockEntityPos
    )
    {
        if (_selectedRecipes.Count == 0) return true;
        var cellCount = Math.Max(1, ___skillItems.Count);
        var columns = Math.Min(cellCount, Core.Config?.AnvilRecipeSelectionColumns ?? 8);
        var rows = (int)Math.Ceiling(cellCount / (double)columns);
        var slotSize = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
        var fixedWidth = Math.Max(300.0, columns * slotSize);
        var gridBounds = ElementBounds.Fixed(0.0, 30.0, fixedWidth, rows * slotSize);
        var nameBounds = ElementBounds.Fixed(0.0, rows * slotSize + 50.0, fixedWidth, 33.0);
        var descBounds = nameBounds.BelowCopy(fixedDeltaY: 10.0);
        var ingredientDescBounds = descBounds.BelowCopy().WithFixedWidth(descBounds.fixedWidth * 0.5);
        var richTextBounds = ingredientDescBounds.RightCopy()
            .WithAlignment(EnumDialogArea.RightFixed)
            .WithFixedOffset(-50, -20)
            .WithFixedPadding(0, 10);
        var dialogBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        dialogBounds.BothSizing = ElementSizing.FitToChildren;

        var dialogName = "toolmodeselect" + ___blockEntityPos;
        __instance.SingleComposer = ___capi.Gui.CreateCompo(dialogName, ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(dialogBounds)
            .AddDialogTitleBar(Lang.Get("Select Recipe"), __instance.OnTitleBarClose())
            .BeginChildElements(dialogBounds)
            .AddSkillItemGrid(___skillItems, columns, rows, __instance.OnSlotClick(), gridBounds, "skillitemgrid")
            .AddDynamicText("", CairoFont.WhiteSmallishText(), nameBounds, "name")
            .AddDynamicText("", CairoFont.WhiteDetailText(), descBounds, "desc")
            .AddDynamicText("", CairoFont.WhiteDetailText(), ingredientDescBounds, "ingredientDesc")
            .AddRichtext("", CairoFont.WhiteDetailText(), richTextBounds, "ingredientCounts")
            .EndChildElements()
            .Compose();

        __instance.SingleComposer.GetSkillItemGrid("skillitemgrid").OnSlotOver = num =>
            OnSlotOver(__instance, ___skillItems, ___prevSlotOver, ___capi, num);
        _selectedRecipes.Clear();
        return false;
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(GuiDialogBlockEntityRecipeSelector), "OnTitleBarClose")]
    private static void OnTitleBarClose_Reverse([UsedImplicitly] GuiDialogBlockEntityRecipeSelector __instance)
    {
        throw new NotImplementedException("Reverse patch stub.");
    }

    private static Action OnTitleBarClose(this GuiDialogBlockEntityRecipeSelector recipeSelector)
    {
        return () => OnTitleBarClose_Reverse(recipeSelector);
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(GuiDialogBlockEntityRecipeSelector), "OnSlotClick")]
    private static void OnSlotClick_Reverse(GuiDialogBlockEntityRecipeSelector __instance, [UsedImplicitly] int num)
    {
        throw new NotImplementedException("Reverse patch stub.");
    }

    private static Action<int> OnSlotClick(this GuiDialogBlockEntityRecipeSelector recipeSelector)
    {
        return num => OnSlotClick_Reverse(recipeSelector, num);
    }

    private static void OnSlotOver(GuiDialogBlockEntityRecipeSelector recipeSelector, List<SkillItem> skillItems,
        int prevSlotOver,
        ICoreClientAPI capi, int num)
    {
        if (num >= skillItems.Count || num == prevSlotOver || num >= _selectedRecipes.Count)
            return;
        var selectedRecipe = _selectedRecipes[num];
        var recipeId = selectedRecipe.RecipeId;
        var outputStack = selectedRecipe.Output.ResolvedItemstack;
        var currentSkillItem = skillItems[num];
        recipeSelector.SingleComposer.GetDynamicText("name").SetNewText(currentSkillItem.Name);
        recipeSelector.SingleComposer.GetDynamicText("desc").SetNewText(currentSkillItem.Description);
        var materialStack = GetSmithingIngredientStack(capi, selectedRecipe.Output.ResolvedItemstack, recipeId);
        if (outputStack == null || _selectedIngredient == null || materialStack == null)
        {
            var text = "";
            if (skillItems[num].Data is ItemStack[] data)
                text = Lang.Get("recipeselector-requiredcount", data[0].StackSize, data[0].GetName().ToLower());
            recipeSelector.SingleComposer.GetDynamicText("ingredientDesc").SetNewText(text);
            recipeSelector.SingleComposer.GetRichtext("ingredientCounts")
                .SetNewText([]);
            return;
        }

        recipeSelector.SingleComposer.GetDynamicText("ingredientDesc")
            .SetNewText(Lang.Get("recipeselector-requiredcount", materialStack.StackSize,
                materialStack.GetName().ToLower()));

        var onStackClickedAction = new Action<ItemStack>(cs =>
            capi.LinkProtocols["handbook"]
                ?.DynamicInvoke(new LinkTextComponent("handbook://" + GuiHandbookItemStackPage.PageCodeForStack(cs))));

        var ingotStackComponent =
            new ItemstackTextComponent(capi, materialStack, 50, 5.0, EnumFloat.Inline, onStackClickedAction)
                { ShowStacksize = true };
        var stackComponentsText = VtmlUtil.Richtextify(capi, "", CairoFont.WhiteDetailText())
            .AddToArray(ingotStackComponent).ToArray();
        recipeSelector.SingleComposer.GetRichtext("ingredientCounts").SetNewText(stackComponentsText);
    }

    public static ItemStack? GetSmithingIngredientStack(
        ICoreClientAPI capi,
        ItemStack stack,
        int recipeId)
    {
        if (_selectedIngredient == null)
            return null;
        var voxelCount = CacheHelper.GetOrAdd(Core.RecipeVoxelCountCache, recipeId,
            () => capi.GetSmithingRecipes().Find(recipe => recipe.RecipeId == recipeId)?.Voxels.VoxelCount() ?? 0);
        var stackSize = GetStackCount(_selectedIngredient.Collectible, voxelCount);
        var adjustedIngredient = _selectedIngredient.Clone();
        adjustedIngredient.SetTemperature(capi.World, 0);
        adjustedIngredient.StackSize = stackSize;
        return adjustedIngredient;
    }

    private static int GetStackCount(CollectibleObject c, int voxelCount)
    {
        var workable = c.GetCollectibleInterface<IAnvilWorkable>();
        return c switch
        {
            _ when workable is not null => CeilDiv(voxelCount, workable.VoxelCountForHandbook(new ItemStack(c))),
            _ => (int)Math.Ceiling(voxelCount * (c.CombustibleProps?.SmeltedRatio ?? 1.0) / 42.0)
        };
    }

    private static int CeilDiv(int numerator, int denominator)
    {
        return (numerator + denominator - 1) / denominator;
    }
}