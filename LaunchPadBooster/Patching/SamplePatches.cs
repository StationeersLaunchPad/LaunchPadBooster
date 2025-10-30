using Assets.Scripts.Objects;
using HarmonyLib;
using UnityEngine;

namespace LaunchPadBooster.Patching
{
    [HarmonyGameVersionPatch("0.0.0.0", "0.2.9999.99999")]
    public static class SamplePatches
    {
        [HarmonyGameVersionPatch("0.0.0.0", "0.2.0.0")]
        [HarmonyPatch(typeof(Item), nameof(Item.OnUseItem)), HarmonyPostfix]
        static void Item_OnUseItem(ref Thing __result, Thing useOnThing)
        {
            Debug.Log(__result.DisplayName);
        }
    }
}