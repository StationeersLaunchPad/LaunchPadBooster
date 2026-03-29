using Assets.Scripts.Networking;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  internal static void WriteUpdatePrefix(RocketBinaryWriter writer)
  {
    var swriter = new SectionedWriter(writer);
    foreach (var net in Instances)
    {
      if (net.UpdatePrefixSerializer is not IUpdatePrefixSerializer serializer)
        continue;
      swriter.StartSection(net.Hash);
      serializer.SerializeUpdatePrefix(writer);
      swriter.FinishSection();
    }
    swriter.Finish();
  }

  internal static void WriteUpdateSuffix(RocketBinaryWriter writer)
  {
    var swriter = new SectionedWriter(writer);
    foreach (var net in Instances)
    {
      if (net.UpdateSuffixSerializer is not IUpdateSuffixSerializer serializer)
        continue;
      swriter.StartSection(net.Hash);
      serializer.SerializeUpdateSuffix(writer);
      swriter.FinishSection();
    }
    swriter.Finish();
  }

  internal static void ReadUpdatePrefix(RocketBinaryReader reader)
  {
    var sreader = new SectionedReader(reader);
    while (sreader.Next(out var modHash, out var section))
    {
      if (!InstancesByHash.TryGetValue(modHash, out var net)
          || net.UpdatePrefixSerializer is not IUpdatePrefixSerializer serializer)
      {
        Debug.LogWarning($"Missing update prefix serializer for {GetModName(modHash)}");
        continue;
      }
      serializer.DeserializeUpdatePrefix(section);
    }
  }

  internal static void ReadUpdateSuffix(RocketBinaryReader reader)
  {
    var sreader = new SectionedReader(reader);
    while (sreader.Next(out var modHash, out var section))
    {
      if (!InstancesByHash.TryGetValue(modHash, out var net)
          || net.UpdateSuffixSerializer is not IUpdateSuffixSerializer serializer)
      {
        Debug.LogWarning($"Missing update suffix serializer for {GetModName(modHash)}");
        continue;
      }
      serializer.DeserializeUpdateSuffix(section);
    }
  }
}