using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;

namespace LaunchPadBooster.Logic;

internal static class LogicReaderPatch
{
    internal static void GetPassiveTooltip(
        LogicReader __instance,
        ref PassiveTooltip __result)
    {
        if (!LogicRegistry.TryGet(__instance.LogicType, out var property))
            return;

        var oldName = __instance.LogicType.ToString();

        __result.Extended = __result.Extended.Replace(
            $".<color=yellow>{oldName}</color> =",
            $".<color=yellow>{property.Name}</color> ="
        );
    }
}