using Assets.Scripts;
using Assets.Scripts.Objects.Motherboards;
using HarmonyLib;

namespace LaunchPadBooster.Logic;

internal static class LogicNetworkPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Network), "GetDataTypeForNetworkSend")]
    internal static bool GetDataTypeForNetworkSend(
        LogicType __0,
        ref byte __result)
    {
        if (!LogicRegistry.TryGet(__0, out var property))
            return true;

        __result = (byte)property.NetworkType;
#if DEBUG
        UnityEngine.Debug.Log(
            $"Custom LogicType {property.Name} ({(ushort)__0}) network type: {property.NetworkType}"); 
#endif
        return false;
    }
}