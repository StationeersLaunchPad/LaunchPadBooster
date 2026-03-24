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

  public readonly ModID ID;

  private Func<string, bool> versionCheck;
  public bool MultiplayerRequired { get; private set; } = false;

  internal readonly List<Type> NetworkMessageTypes = [];
  internal readonly List<Thing> Prefabs = [];
  internal readonly List<IPrefabSetup> Setups = [];
  internal readonly List<Type> SaveDataTypes = [];

  public Mod(string name, string version)
  {
    ID = new(name, version);

    lock (allLock) AllMods.Add(this);
  }

  public void SetVersionCheck(Func<string, bool> versionCheck)
  {
    this.versionCheck = versionCheck;
  }

  public void SetMultiplayerRequired()
  {
    MultiplayerRequired = true;
    ModNetworking.Initialize();
  }

  internal bool VersionValid(string version)
  {
    if (versionCheck != null) return versionCheck(version);
    return version == ID.Version;
  }

  public void AddSaveDataType<T>()
  {
    SaveDataPatch.Initialize();
    SaveDataTypes.Add(typeof(T));
  }

  public void RegisterNetworkMessage<T>() where T : ModNetworkMessage<T>, new()
  {
    SetMultiplayerRequired();
    NetworkMessageTypes.Add(typeof(T));
  }

  public void AddPrefabs(IEnumerable<GameObject> prefabs)
  {
    SetMultiplayerRequired();
    PrefabPatch.Initialize();
    Prefabs.AddRange(prefabs.Select(prefab => prefab.GetComponent<Thing>()).Where(thing => thing != null));
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