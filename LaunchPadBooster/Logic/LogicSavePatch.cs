using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Serialization;
using HarmonyLib;

namespace LaunchPadBooster.Logic;

public sealed class CustomLogicWriterSaveData : LogicWriterSaveData
{
    public string CustomLogicTypeName;
}

public sealed class CustomLogicBatchWriterSaveData : LogicBatchWriterSaveData
{
    public string CustomLogicTypeName;
}

public sealed class CustomLogicWriterSwitchSaveData : LogicWriterSwitchSaveData
{
    public string CustomLogicTypeName;
}

internal static class LogicSavePatch
{
    private static readonly MethodInfo LogicWriterInitialiseSaveData =
        AccessTools.Method(typeof(LogicWriter), "InitialiseSaveData");

    private static readonly MethodInfo LogicBatchWriterInitialiseSaveData =
        AccessTools.Method(typeof(LogicBatchWriter), "InitialiseSaveData");

    private static readonly MethodInfo LogicWriterSwitchInitialiseSaveData =
        AccessTools.Method(typeof(LogicWriterSwitch), "InitialiseSaveData");

    internal static void AddExtraTypes(ref List<Type> __0)
    {
        AddExtraType(__0, typeof(CustomLogicWriterSaveData));
        AddExtraType(__0, typeof(CustomLogicBatchWriterSaveData));
        AddExtraType(__0, typeof(CustomLogicWriterSwitchSaveData));

        LogicMotherboardSavePatch.AddExtraTypes(__0);
    }

    private static void AddExtraType(List<Type> types, Type type)
    {
        if (!types.Contains(type))
            types.Add(type);
    }

    internal static bool SerializeLogicWriter(
        LogicWriter __instance,
        ref ThingSaveData __result)
    {
        if (!LogicRegistry.TryGet(__instance.LogicType, out var property))
            return true;

        ThingSaveData saveData = new CustomLogicWriterSaveData();

        InitialiseSaveData(
            LogicWriterInitialiseSaveData,
            __instance,
            ref saveData);

        var customSaveData = (CustomLogicWriterSaveData)saveData;

        // Never let XmlSerializer see the undefined enum value.
        customSaveData.LogicType = LogicType.None;
        customSaveData.CustomLogicTypeName = property.Name;

        __result = customSaveData;
        return false;
    }

    internal static void DeserializeLogicWriter(
        LogicWriter __instance,
        ThingSaveData __0)
    {
        if (__0 is not CustomLogicWriterSaveData saveData)
            return;

        RestoreLogicType(
            __instance,
            saveData.CustomLogicTypeName);
    }

    internal static bool SerializeLogicBatchWriter(
        LogicBatchWriter __instance,
        ref ThingSaveData __result)
    {
        if (!LogicRegistry.TryGet(__instance.LogicType, out var property))
            return true;

        ThingSaveData saveData = new CustomLogicBatchWriterSaveData();

        InitialiseSaveData(
            LogicBatchWriterInitialiseSaveData,
            __instance,
            ref saveData);

        var customSaveData =
            (CustomLogicBatchWriterSaveData)saveData;

        customSaveData.LogicType = LogicType.None;
        customSaveData.CustomLogicTypeName = property.Name;

        __result = customSaveData;
        return false;
    }

    internal static void DeserializeLogicBatchWriter(
        LogicBatchWriter __instance,
        ThingSaveData __0)
    {
        if (__0 is not CustomLogicBatchWriterSaveData saveData)
            return;

        RestoreLogicType(
            __instance,
            saveData.CustomLogicTypeName);
    }

    internal static bool SerializeLogicWriterSwitch(
        LogicWriterSwitch __instance,
        ref ThingSaveData __result)
    {
        if (!LogicRegistry.TryGet(__instance.LogicType, out var property))
            return true;

        ThingSaveData saveData =
            new CustomLogicWriterSwitchSaveData();

        InitialiseSaveData(
            LogicWriterSwitchInitialiseSaveData,
            __instance,
            ref saveData);

        var customSaveData =
            (CustomLogicWriterSwitchSaveData)saveData;

        customSaveData.LogicType = LogicType.None;
        customSaveData.CustomLogicTypeName = property.Name;

        __result = customSaveData;
        return false;
    }

    internal static void DeserializeLogicWriterSwitch(
        LogicWriterSwitch __instance,
        ThingSaveData __0)
    {
        if (__0 is not CustomLogicWriterSwitchSaveData saveData)
            return;

        RestoreLogicType(
            __instance,
            saveData.CustomLogicTypeName);
    }

    private static void RestoreLogicType(
        LogicWriterBase writer,
        string name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        if (!LogicRegistry.TryGet(name, out var property))
            return;

        writer.LogicType = property.LogicType;
    }

    private static void InitialiseSaveData(
        MethodInfo method,
        object instance,
        ref ThingSaveData saveData)
    {
        var arguments = new object[] { saveData };

        method.Invoke(instance, arguments);

        saveData = (ThingSaveData)arguments[0];
    }

    internal static void ApplyPatches(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(
                typeof(XmlSaveLoad),
                "AddExtraTypes"
            ),
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicSavePatch),
                    nameof(AddExtraTypes)
                )
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(LogicWriter),
                nameof(LogicWriter.SerializeSave)
            ),
            prefix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicSavePatch),
                    nameof(SerializeLogicWriter)
                )
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(LogicWriter),
                nameof(LogicWriter.DeserializeSave)
            ),
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicSavePatch),
                    nameof(DeserializeLogicWriter)
                )
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(LogicBatchWriter),
                nameof(LogicBatchWriter.SerializeSave)
            ),
            prefix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicSavePatch),
                    nameof(SerializeLogicBatchWriter)
                )
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(LogicBatchWriter),
                nameof(LogicBatchWriter.DeserializeSave)
            ),
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicSavePatch),
                    nameof(DeserializeLogicBatchWriter)
                )
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(LogicWriterSwitch),
                nameof(LogicWriterSwitch.SerializeSave)
            ),
            prefix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicSavePatch),
                    nameof(SerializeLogicWriterSwitch)
                )
            )
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(LogicWriterSwitch),
                nameof(LogicWriterSwitch.DeserializeSave)
            ),
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(LogicSavePatch),
                    nameof(DeserializeLogicWriterSwitch)
                )
            )
        );
    }
}