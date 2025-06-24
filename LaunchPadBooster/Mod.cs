using System;
using System.Collections.Generic;
using LaunchPadBooster.Networking;

namespace LaunchPadBooster
{
  public sealed class Mod
  {
    private static readonly object allLock = new();
    public static readonly List<Mod> AllMods = new();

    public readonly ModID ID;

    private bool _multiplayerRequired = false;
    private Func<string, bool> _versionCheck;
    public bool MultiplayerRequired => _multiplayerRequired;

    private readonly List<Type> _networkMessageTypes = new();
    public IEnumerable<Type> NetworkMessageTypes => _networkMessageTypes;

    public Mod(string name, string version)
    {
      this.ID = new(name, version);

      lock (allLock) AllMods.Add(this);
    }

    public void SetMultiplayerRequired(Func<string, bool> versionCheck = null)
    {
      this._multiplayerRequired = true;
      this._versionCheck = versionCheck;
      ModNetworking.Initialize();
    }

    public bool VersionValid(string version)
    {
      if (_versionCheck != null) return _versionCheck(version);
      return version == this.ID.Version;
    }

    public void RegisterNetworkMessage<T>() where T : ModNetworkMessage<T>, new()
    {
      if (!MultiplayerRequired)
        throw new Exception("Must set multiplayer required to add network messages");
      this._networkMessageTypes.Add(typeof(T));
    }
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