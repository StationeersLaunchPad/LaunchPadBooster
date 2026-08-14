using Assets.Scripts.Objects.Motherboards;

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