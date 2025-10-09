using HarmonyLib;
using JetBrains.Annotations;
using SmithingPlus.Util;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SmithingPlus.Common;

[HarmonyPatch(typeof(DialogueComponent))]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[HarmonyPatchCategory(Core.AlwaysPatchCategory)]
public class DialogueComponentsPatches
{
    // Add dialogue ignored attributes
    // Otherwise npcs get very picky on what tools they want
    [HarmonyPostfix]
    [HarmonyPatch("getIgnoreAttrs")]
    public static void Postfix(ref string[] __result)
    {
        __result = __result.Append(ModStackAttributes.DialogueIgnoredAttributes);
    }
}