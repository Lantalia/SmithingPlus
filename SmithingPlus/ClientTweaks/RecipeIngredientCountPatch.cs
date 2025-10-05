#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GuiDialogBlockEntityRecipeSelector))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch([
        typeof(string),
        typeof(ItemStack[]),
        typeof(Action<int>),
        typeof(Action),
        typeof(BlockPos),
        typeof(ICoreClientAPI)
    ])]
    public static IEnumerable<CodeInstruction> RecipeSelectorCtor_Transpile_SetupDialog(
        IEnumerable<CodeInstruction> instructions)
    {
        var codeInstructions = instructions.ToList();
        // The original target to remove: instance void ...::SetupDialog()
        var setupDialogMethod = AccessTools.Method(
            typeof(GuiDialogBlockEntityRecipeSelector),
            "SetupDialog", []);

        var codeMatcher = new CodeMatcher(codeInstructions)
            .End()
            .MatchEndBackwards(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(ci => ci.Calls(setupDialogMethod))
            );
        if (codeMatcher.IsValid)
            // don't call the original SetupDialog
            codeMatcher.SetOpcodeAndAdvance(OpCodes.Pop);
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiDialogBlockEntityRecipeSelector))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch([
        typeof(string),
        typeof(ItemStack[]),
        typeof(Action<int>),
        typeof(Action),
        typeof(BlockPos),
        typeof(ICoreClientAPI)
    ])]
    public static void CustomSetupDialog(
        string DialogTitle,
        ItemStack[] recipeOutputs,
        Action<int> onSelectedRecipe,
        Action onCancelSelect,
        BlockPos blockEntityPos,
        ICoreClientAPI capi,
        GuiDialogBlockEntityRecipeSelector __instance,
        List<SkillItem> ___skillItems,
        int ___prevSlotOver
    )
    {
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

        var dialogName = "toolmodeselect" + blockEntityPos;
        __instance.SingleComposer = capi.Gui.CreateCompo(dialogName, ElementStdBounds.AutosizedMainDialog)
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
            OnSlotOver(__instance, ___skillItems, ___prevSlotOver, capi, num);
    }

    public static void CustomSetCounts(
        GuiDialogBlockEntityRecipeSelector? dlg,
        int index,
        SmithingRecipe recipe
    )
    {
        if (dlg?.GetField<List<SkillItem>>("skillItems") is null)
            return;
        dlg.GetField<List<SkillItem>>("skillItems")[index].Data = recipe;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlockEntityAnvil), "OpenDialog")]
    public static void OpenDialog_Postfix(ItemStack ingredient, GuiDialog ___dlg)
    {
        var collectibleInterface = ingredient.Collectible.GetCollectibleInterface<IAnvilWorkable>();
        List<SmithingRecipe> recipes = collectibleInterface.GetMatchingRecipes(ingredient);
        for (int index = 0; index < recipes.Count; ++index)
        {
            CustomSetCounts(___dlg as GuiDialogBlockEntityRecipeSelector, index, recipes[index]);
        }
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

    private static void OnSlotOver(
        GuiDialogBlockEntityRecipeSelector recipeSelector,
        List<SkillItem> skillItems,
        int prevSlotOver,
        ICoreClientAPI capi,
        int num)
    {
        if (num >= skillItems.Count || num == prevSlotOver)
            return;
        var currentSkillItem = skillItems[num];
        recipeSelector.SingleComposer.GetDynamicText("name").SetNewText(currentSkillItem.Name);
        recipeSelector.SingleComposer.GetDynamicText("desc").SetNewText(currentSkillItem.Description);

        var text = "";
        ItemStack? materialStack = null;
        if (skillItems[num].Data is ItemStack[] data)
        {
            materialStack = data[0];
        }
        if (skillItems[num].Data is SmithingRecipe recipe)
        {
            materialStack = GetAdjustedIngredientStack(capi, recipe.Ingredient.ResolvedItemstack, recipe.RecipeId);
        }
        if (materialStack != null){
            text = Lang.Get("recipeselector-requiredcount", materialStack.StackSize,
                materialStack.GetName().ToLower());
            var onStackClickedAction = new Action<ItemStack>(cs =>
                capi.LinkProtocols["handbook"]
                    ?.DynamicInvoke(
                        new LinkTextComponent("handbook://" + GuiHandbookItemStackPage.PageCodeForStack(cs))));
            var ingotStackComponent =
                new ItemstackTextComponent(capi, materialStack, 50, 5.0, EnumFloat.Inline, onStackClickedAction)
                    { ShowStacksize = true };
            var stackComponentsText = VtmlUtil.Richtextify(capi, "", CairoFont.WhiteDetailText())
                .AddToArray(ingotStackComponent).ToArray();
            recipeSelector.SingleComposer.GetRichtext("ingredientCounts").SetNewText(stackComponentsText);
        }
        recipeSelector.SingleComposer.GetDynamicText("ingredientDesc").SetNewText(text);
    }
    
    public static ItemStack GetAdjustedIngredientStack(
        ICoreClientAPI capi,
        ItemStack ingredientStack,
        int recipeId)
    {
        var voxelCount = CacheHelper.GetOrAdd(Core.RecipeVoxelCountCache, recipeId,
                () => capi.GetSmithingRecipes().Find(recipe => recipe.RecipeId == recipeId)?.Voxels.VoxelCount() ?? 0);
        if (voxelCount <= 0)
            return ingredientStack;
        var stackSize = GetStackCount(ingredientStack.Collectible, voxelCount);
        var adjustedIngredient = ingredientStack.Clone();
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