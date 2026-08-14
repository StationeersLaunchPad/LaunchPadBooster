using System;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using UnityEngine;

namespace LaunchPadBooster.Logic;

internal sealed class LogicScriptEnum : IScriptEnum
{
    private readonly LogicPropertyInfo[] _properties;

    internal LogicScriptEnum()
    {
        _properties = LogicRegistry.Properties.ToArray();
    }

    public void Execute(
        ref bool isValueSet,
        ref double value,
        string code,
        InstructionInclude propertiesToUse)
    {
        if (isValueSet ||
            (propertiesToUse & InstructionInclude.LogicType) == InstructionInclude.None)
            return;

        if (!LogicRegistry.TryGet(code, out var property))
            return;

        value = (ushort)property.LogicType;
        isValueSet = true;
    }

    public void Execute(
        ref bool isValueSet,
        ref int value,
        string code,
        InstructionInclude propertiesToUse)
    {
        if (isValueSet ||
            (propertiesToUse & InstructionInclude.LogicType) == InstructionInclude.None)
            return;

        if (!LogicRegistry.TryGet(code, out var property))
            return;

        value = (ushort)property.LogicType;
        isValueSet = true;
    }

    public void Parse(ref string masterString)
    {
        foreach (var property in _properties)
        {
            masterString = masterString.ReplaceWholeWord(
                property.Name,
                $"<color=orange>{property.Name}</color>");
        }
    }

    public bool TryParse(string searchText)
    {
        return LogicRegistry.TryGet(searchText, out _);
    }

    public int Count() => _properties.Length;

    public bool IsDeprecated(int i) => false;

    public bool IsHashType(int hash)
    {
        return hash == Animator.StringToHash(nameof(LogicType));
    }

    public HelpReference MakePage(
        int i,
        HelpReference prefab,
        RectTransform parent)
    {
        // Temp placeholder to avoid referencing TMP_Text
        return null;
    }
}