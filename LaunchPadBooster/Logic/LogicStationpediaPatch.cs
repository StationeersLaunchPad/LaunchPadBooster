using Assets.Scripts.Objects.Motherboards;

namespace LaunchPadBooster.Logic;

internal static class LogicStationpediaPatch
{
    internal static bool ParseHelpText(
        string __0,
        ref string __result)
    {
        const string prefix = "{LOGICTYPE:";

        if (!__0.StartsWith(prefix) || !__0.EndsWith("}"))
            return true;

        var value = __0.Substring(
            prefix.Length,
            __0.Length - prefix.Length - 1);

        if (!ushort.TryParse(value, out var id))
            return true;

        if (!LogicRegistry.TryGet((LogicType)id, out var property))
            return true;

        __result =
            $"<link=LogicType{property.Name}><color=orange>{property.Name}</color></link>";

        return false;
    }
    
    internal static bool GetLogicDescription(
        LogicType __0,
        ref string __result)
    {
        if (!LogicRegistry.TryGet(__0, out var property))
            return true;

        __result = property.Name;
        return false;
    }
}