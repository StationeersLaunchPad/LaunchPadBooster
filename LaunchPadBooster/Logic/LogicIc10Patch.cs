using Assets.Scripts.Objects.Motherboards;

namespace LaunchPadBooster.Logic;

internal static class LogicIc10Patch
{
    internal static bool GetLogicType(
        string value,
        ref LogicType __result)
    {
        if (!LogicRegistry.TryGet(value, out var property))
            return true;

        __result = property.LogicType;
        return false;
    }
}