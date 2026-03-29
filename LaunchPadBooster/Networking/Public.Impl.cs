
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking : IModNetworking
{
  internal static readonly List<ModNetworking> Instances = [];
  internal static readonly Dictionary<int, ModNetworking> InstancesByHash = [];

  internal readonly Mod mod;

  internal string ModID => mod.ID.Name;
  internal string Version => mod.ID.Version;
  internal int Hash => mod.Hash;

  internal ModNetworking(Mod mod)
  {
    Initialize();
    this.mod = mod;
    VersionValidator = new DefaultVersionValidator(mod);
    Instances.Add(this);
    InstancesByHash.Add(mod.Hash, this);
  }

  public bool Required
  {
    get;
    set
    {
      if (!value && HasPrefabs)
      {
        Debug.LogWarning($"{mod.ID.Name} is required in multiplayer as it contains prefabs");
        return;
      }
      field = value;
    }
  }
  internal bool HasPrefabs
  {
    get => field;
    set
    {
      field = value;
      if (value)
        Required = true;
    }
  }

  public IVersionValidator VersionValidator { get; set => field = value ?? new DefaultVersionValidator(mod); }

  public void RegisterMessage<T>() where T : INetworkMessage, new() => RegisterMessage<T>(mod);
  public void RegisterRPC<T>() where T : INetworkRPC, new() => RegisterRPC<T>(mod);

  public IJoinValidator JoinValidator { get; set; }

  public IJoinPrefixSerializer JoinPrefixSerializer { get; set; }
  public IJoinSuffixSerializer JoinSuffixSerializer { get; set; }

  public IUpdatePrefixSerializer UpdatePrefixSerializer { get; set; }
  public IUpdateSuffixSerializer UpdateSuffixSerializer { get; set; }

  public void RegisterLegacyMessage<T>() where T : ModNetworkMessage<T>, new() =>
    RegisterLegacyMessage<T>(mod);
}

internal class DefaultVersionValidator(Mod mod) : IVersionValidator
{
  public bool ValidateVersion(string version) => version == mod.ID.Version;
}

internal class LambdaVersionValidator(Func<string, bool> lambda) : IVersionValidator
{
  public bool ValidateVersion(string version) => lambda(version);
}