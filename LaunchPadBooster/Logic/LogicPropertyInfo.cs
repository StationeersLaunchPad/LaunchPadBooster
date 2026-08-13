using Assets.Scripts.Objects.Motherboards;

namespace LaunchPadBooster.Logic;

public sealed class LogicPropertyInfo
{
    public string Name { get; }
    public LogicNetworkType NetworkType { get; }
    public LogicType LogicType { get; internal set; }

    internal LogicPropertyInfo(string name, LogicNetworkType networkType)
    {
        Name = name;
        NetworkType = networkType;
        LogicType = LogicType.None;
    }

    public static implicit operator LogicType(LogicPropertyInfo property)
        => property.LogicType;
}