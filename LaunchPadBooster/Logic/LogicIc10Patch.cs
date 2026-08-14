using Assets.Scripts.Objects.Motherboards;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LaunchPadBooster.Logic;

internal static class LogicIc10Patch
{
    internal static bool GetLogicType(
        string __0,
        ref LogicType __result)
    {
        Debug.Log($"IC10 GetLogicType: '{__0}'");

        if (!LogicRegistry.TryGet(__0, out var property))
        {
            Debug.Log($"IC10 custom logic NOT found: '{__0}'");
            return true;
        }

        __result = property.LogicType;
        Debug.Log($"IC10 custom logic resolved: '{__0}' -> {(ushort)__result}");
        return false;
    }
    
    internal static void FormatLogicTypes(ref string __0)
    {
        foreach (var property in LogicRegistry.Properties)
        {
            __0 = Regex.Replace(
                __0,
                $@"\b{Regex.Escape(property.Name)}\b",
                $"<color=orange>{property.Name}</color>"
            );
        }
    }
    
    internal static bool GetTypeOf(
        string __0,
        ref LogicType __result)
    {
        if (!LogicRegistry.TryGet(__0, out var property))
            return true;

        __result = property.LogicType;
        return false;
    }
}