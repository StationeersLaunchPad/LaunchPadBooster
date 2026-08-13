using Assets.Scripts.Objects.Motherboards;
using System.Text.RegularExpressions;

namespace LaunchPadBooster.Logic;

internal static class LogicIc10Patch
{
    internal static bool GetLogicType(
        string __0,
        ref LogicType __result)
    {
        if (!LogicRegistry.TryGet(__0, out var property))
            return true;

        __result = property.LogicType;
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
}