using Assets.Scripts;
using Assets.Scripts.Objects.Motherboards;
using HarmonyLib;

namespace LaunchPadBooster.Logic;

internal static class LogicNetworkPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Network), "GetDataTypeForNetworkSend")]
    private static bool GetDataTypeForNetworkSend(
        LogicType logicType,
        ref byte __result)
    {
        if (!LogicRegistry.TryGet(logicType, out var property))
            return true;

        __result = (byte)property.NetworkType;
        return false;
    }
}