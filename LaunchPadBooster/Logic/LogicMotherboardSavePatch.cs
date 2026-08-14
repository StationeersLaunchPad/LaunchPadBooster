using System;
using System.Collections.Generic;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Motherboards;
using HarmonyLib;
using System.Linq;
using Assets.Scripts.UI.Motherboard;

namespace LaunchPadBooster.Logic;

public sealed class CustomLogicActionSave : LogicActionSave
{
    public string CustomLogicTypeName;
}

public sealed class CustomLogicConditionSave : LogicConditionSave
{
    public string CustomLogicTypeName;
}

internal static class LogicMotherboardSavePatch
{
    internal static void AddExtraTypes(List<Type> types)
    {
        AddExtraType(types, typeof(CustomLogicActionSave));
        AddExtraType(types, typeof(CustomLogicConditionSave));
    }

    private static void AddExtraType(List<Type> types, Type type)
    {
        if (!types.Contains(type))
            types.Add(type);
    }

    internal static void SerializeLogicMotherboard(
        ref ThingSaveData __result)
    {
        if (__result is not LogicMotherboardSaveData saveData)
            return;

        foreach (var state in saveData.LogicStates)
        {
            if (state.Actions != null)
            {
                for (var i = 0; i < state.Actions.Count; i++)
                {
                    var action = state.Actions[i];

                    if (!LogicRegistry.TryGet(
                            action.Type,
                            out var property))
                        continue;

                    state.Actions[i] = new CustomLogicActionSave
                    {
                        DeviceReferenceId = action.DeviceReferenceId,
                        Type = LogicType.None,
                        Value = action.Value,
                        CustomLogicTypeName = property.Name
                    };
                }
            }

            if (state.Conditions != null)
            {
                for (var i = 0; i < state.Conditions.Count; i++)
                {
                    var condition = state.Conditions[i];

                    if (!LogicRegistry.TryGet(
                            condition.Type,
                            out var property))
                        continue;

                    state.Conditions[i] =
                        new CustomLogicConditionSave
                        {
                            DeviceReferenceId =
                                condition.DeviceReferenceId,

                            Type = LogicType.None,
                            Operation = condition.Operation,
                            Value = condition.Value,
                            IsDisconnected =
                                condition.IsDisconnected,
                            IsTrue = condition.IsTrue,

                            CustomLogicTypeName =
                                property.Name
                        };
                }
            }
        }
    }

    internal static void DeserializeLogicMotherboard(
        ThingSaveData __0)
    {
        if (__0 is not LogicMotherboardSaveData saveData)
            return;

        foreach (var state in saveData.LogicStates)
        {
            if (state.Actions != null)
            {
                foreach (var action in state.Actions)
                {
                    if (action is not CustomLogicActionSave custom)
                        continue;

                    if (!LogicRegistry.TryGet(
                            custom.CustomLogicTypeName,
                            out var property))
                        continue;

                    custom.Type = property.LogicType;
                }
            }

            if (state.Conditions != null)
            {
                foreach (var condition in state.Conditions)
                {
                    if (condition
                        is not CustomLogicConditionSave custom)
                        continue;

                    if (!LogicRegistry.TryGet(
                            custom.CustomLogicTypeName,
                            out var property))
                        continue;

                    custom.Type = property.LogicType;
                }
            }
        }
        
    }

    internal static void ExtendLogicTypes()
    {
        ScreenDropdownBase.LogicTypes =
            Enum.GetValues(typeof(LogicType))
                .Cast<LogicType>()
                .Concat(
                    LogicRegistry.Properties
                        .Select(x => x.LogicType))
                .ToArray();
    }
    
    internal static void ApplyPatches(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(
                typeof(LogicMotherboard),
                nameof(LogicMotherboard.SerializeSave)
            ),
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicMotherboardSavePatch),
                    nameof(SerializeLogicMotherboard)
                )
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(LogicMotherboard),
                nameof(LogicMotherboard.DeserializeSave),
                new[] { typeof(ThingSaveData) }
            ),
            prefix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicMotherboardSavePatch),
                    nameof(DeserializeLogicMotherboard)
                )
            )
        );
    }
}