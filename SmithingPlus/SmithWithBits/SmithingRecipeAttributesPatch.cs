using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SmithingPlus.Common;
using SmithingPlus.Util;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace SmithingPlus.SmithWithBits;

[HarmonyPatch]
public class SmithingRecipeAttributesPatch
{
    public static void GetMatchingRecipes_Postfix(IAnvilWorkable __instance, ref List<SmithingRecipe> __result,
        ItemStack stack)
    {
        if (__instance is ItemWorkItem) return; // Return for existing work item
        if (__instance is not CollectibleBehaviorWorkableNugget)
            __result = __result.Where(r =>
                r.Ingredient.RecipeAttributes?[ModRecipeAttributes.NuggetRecipe]?.AsBool() != true
            ).ToList();
        if (__instance is not CollectibleBehaviorAnvilWorkable)
            __result = __result.Where(r =>
                r.Ingredient.RecipeAttributes?[ModRecipeAttributes.WorkableRecipe]?.AsBool() != true
            ).ToList();
        __result = __result.Where(r => r.Ingredient.RecipeAttributes?[ModRecipeAttributes.RepairOnly]?.AsBool() != true
        ).ToList();
    }

    public static void PatchIfEnabled(bool condition, Harmony harmony)
    {
        if (!condition) return;
        var interfaceType = typeof(IAnvilWorkable);

        // Look through all loaded assemblies and their types
        var allTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    return e.Types.Where(t => t != null);
                }
            });

        var targets = allTypes
            .Where(t => interfaceType.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

        var seen = new HashSet<(Module, int)>();
        foreach (var type in targets)
        {
            // Find the method GetMatchingRecipes on that type
            var method = type.GetMethod("GetMatchingRecipes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null || method.IsAbstract) continue;

            var target = method.IsVirtual ? method.GetBaseDefinition() : method;

            var postfix =
                new HarmonyMethod(typeof(SmithingRecipeAttributesPatch).GetMethod(nameof(GetMatchingRecipes_Postfix)));
            // Apply Harmony patch to it
            if (seen.Add((target.Module, target.MetadataToken)))
                harmony.Patch(target, postfix: postfix);
        }
    }
}