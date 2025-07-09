using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using LaunchPadBooster.Networking;
using UnityEngine;

namespace LaunchPadBooster
{
  public sealed class Mod
  {
    private static readonly object allLock = new();
    public static readonly List<Mod> AllMods = new();

    public readonly ModID ID;

    private Func<string, bool> _versionCheck;
    public bool MultiplayerRequired { get; private set; } = false;

    internal readonly List<Type> NetworkMessageTypes = new();
    internal readonly List<Thing> Prefabs = new();
    internal readonly List<IPrefabSetup> Setups = new();
    internal readonly List<Type> SaveDataTypes = new();

    public Mod(string name, string version)
    {
      this.ID = new(name, version);

      lock (allLock) AllMods.Add(this);
    }

    public void SetVersionCheck(Func<string, bool> versionCheck)
    {
      this._versionCheck = versionCheck;
    }

    public void SetMultiplayerRequired()
    {
      this.MultiplayerRequired = true;
      ModNetworking.Initialize();
    }

    internal bool VersionValid(string version)
    {
      if (_versionCheck != null) return _versionCheck(version);
      return version == this.ID.Version;
    }

    public void AddSaveDataType<T>()
    {
      SaveDataPatch.Initialize();
      this.SaveDataTypes.Add(typeof(T));
    }

    public void RegisterNetworkMessage<T>() where T : ModNetworkMessage<T>, new()
    {
      SetMultiplayerRequired();
      this.NetworkMessageTypes.Add(typeof(T));
    }

    public void AddPrefabs(IEnumerable<GameObject> prefabs)
    {
      SetMultiplayerRequired();
      PrefabPatch.Initialize();
      this.Prefabs.AddRange(prefabs.Select(prefab => prefab.GetComponent<Thing>()).Where(thing => thing != null));
    }

    public PrefabSetup<T> SetupPrefabs<T>(string name = null)
    {
      var setup = new PrefabSetup<T>(name);
      this.Setups.Add(setup);
      return setup;
    }

    public PrefabSetup<Thing> SetupPrefabs(string name = null) => SetupPrefabs<Thing>(name);
  }

  public readonly struct ModID
  {
    public readonly string Name;
    public readonly string Version;

    public ModID(string name, string version)
    {
      this.Name = name;
      this.Version = version;
    }
  }
}