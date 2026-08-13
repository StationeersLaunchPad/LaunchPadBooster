using System;
using System.Collections.Generic;

namespace LaunchPadBooster.Logic;

internal static class LogicRegistry
{
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

    internal static LogicPropertyInfo Add(
        Mod mod,
        string name,
        LogicNetworkType networkType)
    {
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
}