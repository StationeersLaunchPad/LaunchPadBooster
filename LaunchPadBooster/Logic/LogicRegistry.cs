using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects.Motherboards;
using HarmonyLib;
using LaunchPadBooster.Utils;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts;

namespace LaunchPadBooster.Logic;

internal static class LogicRegistry
{
    private static readonly object InitLock = new();
    private static bool _initialized;
    
    private static readonly Dictionary<LogicType, LogicPropertyInfo> ByLogicType = new();
    
    internal static void Initialize()
    {
        lock (InitLock)
        {
            if (_initialized)
                return;

            var harmony = new Harmony("LaunchPadBooster.LogicRegistry");

            harmony.Patch(
                ReflectionUtils.Method(() => Prefab.LoadAll()),
                prefix: new HarmonyMethod(
                    ReflectionUtils.Method(() => FinalizeRegistry()))
            );
            
            harmony.Patch(
                AccessTools.Method(typeof(Network), "GetDataTypeForNetworkSend"),
                prefix: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(LogicNetworkPatch),
                        nameof(LogicNetworkPatch.GetDataTypeForNetworkSend)
                    )
                )
            );
            
            harmony.Patch(
                AccessTools.Method(
                    AccessTools.TypeByName("_Operation"),
                    "_GetLogicType"
                ),
                prefix: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(LogicIc10Patch),
                        nameof(LogicIc10Patch.GetLogicType)
                    )
                )
            );

            harmony.Patch(
                AccessTools.Method(typeof(Localization), "ReplaceCommands"),
                postfix: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(LogicIc10Patch),
                        nameof(LogicIc10Patch.FormatLogicTypes)
                    )
                )
            );
            
            _initialized = true;
        }
    }
    
    private sealed class Entry
    {
        public LogicPropertyInfo Property { get; }
        public HashSet<Mod> Owners { get; } = new();

        public Entry(LogicPropertyInfo property)
        {
            Property = property;
        }
    }

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    internal static IEnumerable<LogicPropertyInfo> Properties => Entries.Values.Select(x => x.Property);

    private static bool _finalized;
    
    internal static LogicPropertyInfo Add(
        Mod mod,
        string name,
        LogicNetworkType networkType)
    {
        if (_finalized) // finalized guard
            throw new InvalidOperationException(
                "Logic types cannot be modified after finalization.");
        
        if (Entries.TryGetValue(name, out var entry))
        {
            if (entry.Property.NetworkType != networkType)
                throw new InvalidOperationException(
                    $"Logic type '{name}' is already registered as " +
                    $"{entry.Property.NetworkType}.");

            entry.Owners.Add(mod);
            return entry.Property;
        }

        var property = new LogicPropertyInfo(name, networkType);
        entry = new Entry(property);
        entry.Owners.Add(mod);

        Entries.Add(name, entry);

        return property;
    }

    internal static bool Remove(Mod mod, LogicPropertyInfo property)
    {
        if (_finalized) // finalized guard
            throw new InvalidOperationException(
                "Logic types cannot be modified after finalization.");
        
        if (!Entries.TryGetValue(property.Name, out var entry))
            return false;

        if (!ReferenceEquals(entry.Property, property))
            return false;

        if (!entry.Owners.Remove(mod))
            return false;

        if (entry.Owners.Count == 0)
            Entries.Remove(property.Name);

        return true;
    }
    
    internal static void FinalizeRegistry()
    {
        if (_finalized)
            return;

        ushort nextId = (ushort)(
            Enum.GetValues(typeof(LogicType))
                .Cast<LogicType>()
                .Max(x => (ushort)x) + 1);

        foreach (var entry in Entries.Values
                     .OrderBy(x => x.Property.Name, StringComparer.Ordinal))
        {
            entry.Property.LogicType = (LogicType)nextId++;
            ByLogicType.Add(entry.Property.LogicType, entry.Property);
        }

        Logicable.LogicTypes = Logicable.LogicTypes
            .Concat(Entries.Values.Select(x => x.Property.LogicType))
            .ToArray();
        
        _finalized = true;
    }

    internal static bool TryGet(
        LogicType logicType,
        out LogicPropertyInfo property)
    {
        return ByLogicType.TryGetValue(logicType, out property);
    }
    
    internal static bool TryGet(
        string name,
        out LogicPropertyInfo property)
    {
        if (Entries.TryGetValue(name, out var entry))
        {
            property = entry.Property;
            return true;
        }

        property = null;
        return false;
    }
    
}