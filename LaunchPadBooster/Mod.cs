using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using LaunchPadBooster.Networking;
using UnityEngine;

namespace LaunchPadBooster;

public sealed class Mod
{
  private static readonly object allLock = new();
  public static readonly List<Mod> AllMods = [];
  internal static readonly Dictionary<int, Mod> ModsByHash = [];

  public readonly ModID ID;
  internal readonly int Hash;

  [Obsolete("Use Mod.Networking.Required instead", true)]
  public bool MultiplayerRequired => Networking.Required;

  internal readonly List<Thing> Prefabs = [];
  internal readonly List<IPrefabSetup> Setups = [];
  internal readonly List<Type> SaveDataTypes = [];

  public IModNetworking Networking => field ??= new ModNetworking(this);

  public Mod(string name, string version)
  {
    ID = new(name, version);

    Hash = Animator.StringToHash(name);

    lock (allLock)
    {
      ModsByHash.Add(Hash, this);
      AllMods.Add(this);
    }
  }

  [Obsolete("""
    Use Mod.Networking.VersionValidator instead, or implement IJoinValidator for custom join validation
  """, true)]
  public void SetVersionCheck(Func<string, bool> versionCheck)
  {
    Networking.VersionValidator = new LambdaVersionValidator(versionCheck);
  }

  [Obsolete("Use Mod.Networking.Required instead", true)]
  public void SetMultiplayerRequired()
  {
    Networking.Required = true;
  }

  public void AddSaveDataType<T>()
  {
    SaveDataPatch.Initialize();
    SaveDataTypes.Add(typeof(T));
  }

  [Obsolete("""
    Implement INetworkMessage and use Mod.Networking.RegisterMessage instead.
    Use Mod.Networking.RegisterLegacyMessage to use the legacy mod messsage class for now
  """, true)]
  public void RegisterNetworkMessage<T>() where T : ModNetworkMessage<T>, new()
  {
    Networking.RegisterLegacyMessage<T>();
  }

  public void AddPrefabs(IEnumerable<GameObject> prefabs)
  {
    var thingPrefabs =
      prefabs.Select(prefab => prefab.GetComponent<Thing>()).Where(thing => thing != null).ToList();
    if (thingPrefabs.Count == 0)
      return;
    (Networking as ModNetworking).HasPrefabs = true;
    PrefabPatch.Initialize();
    Prefabs.AddRange(thingPrefabs);
  }

  public PrefabSetup<T> SetupPrefabs<T>(string name = null)
  {
    var setup = new PrefabSetup<T>(name);
    Setups.Add(setup);
    return setup;
  }

  public PrefabSetup<Thing> SetupPrefabs(string name = null) => SetupPrefabs<Thing>(name);
}

public readonly struct ModID(string name, string version)
{
  public readonly string Name = name;
  public readonly string Version = version;
}