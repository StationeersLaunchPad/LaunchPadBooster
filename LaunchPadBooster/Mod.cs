using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using LaunchPadBooster;
using LaunchPadBooster.Events;
using LaunchPadBooster.Networking;
using LaunchPadBooster.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    public event Action<ModEvent> OnEvent;

    public Mod(string name, string version)
    {
      this.ID = new(name, version);

      lock (allLock)
        AllMods.Add(this);

      this.RegisterEvents();
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

    public void AddSaveDataType(Type type)
    {
      this.SaveDataTypes.Add(type);
    }

    public void AddSaveDataType<T>()
    {
      this.AddSaveDataType(typeof(T));
    }

    public void RegisterNetworkMessage<T>() where T : ModNetworkMessage<T>, new()
    {
      this.SetMultiplayerRequired();
      this.NetworkMessageTypes.Add(typeof(T));
    }

    public void AddPrefabs(IEnumerable<GameObject> prefabs)
    {
      this.SetMultiplayerRequired();
      PrefabPatch.Initialize();
      this.Prefabs.AddRange(prefabs.Select(prefab => prefab.GetComponent<Thing>()).Where(thing => thing != null));
    }

    public PrefabSetup<T> SetupPrefabs<T>(string name = null)
    {
      var setup = new PrefabSetup<T>(name);
      this.Setups.Add(setup);
      return setup;
    }

    public PrefabSetup<Thing> SetupPrefabs(string name = null) => this.SetupPrefabs<Thing>(name);

    internal void RegisterEvents()
    {
      ModEvents.Initialize();

      ModEventPatches.OnMenuPageEnabled += this.ModEventPatches_OnMenuPageEnabled;
      ModEventPatches.OnMenuPageDisabled += this.ModEventPatches_OnMenuPageDisabled;
      SceneManager.sceneLoaded += this.SceneManager_sceneLoaded;
      SceneManager.sceneUnloaded += this.SceneManager_sceneUnloaded;
    }

    private void ModEventPatches_OnMenuPageEnabled(string page)
    {

      MenuPageOpenedEvent pageEvent = page switch
      {
        Constants.MAIN_MENU_PAGE => new MainMenuOpenedEvent { Name = page },
        Constants.NEW_GAME_PAGE => new NewGameMenuOpenedEvent { Name = page },
        Constants.LOAD_GAME_PAGE => new LoadGameMenuOpenedEvent { Name = page },
        Constants.DIFFICULTY_SELECTION_PAGE => new DifficultyMenuOpenedEvent { Name = page },
        Constants.STARTING_CONDITIONS_PAGE => new StartConditionsMenuOpenedEvent { Name = page },
        Constants.TUTORIALS_PAGE => new TutorialsMenuOpenedEvent { Name = page },
        Constants.MULTIPLAYER_PAGE => new MultiplayerMenuOpenedEvent { Name = page },
        Constants.WORKSHOP_PAGE => new WorkshopMenuOpenedEvent { Name = page },
        Constants.SETTINGS_PAGE => new SettingsMenuOpenedEvent { Name = page },
        _ => throw new NotImplementedException(),
      };
      this.OnEvent?.Invoke(pageEvent);
    }

    private void ModEventPatches_OnMenuPageDisabled(string page)
    {
      MenuPageClosedEvent pageEvent = page switch
      {
        Constants.MAIN_MENU_PAGE => new MainMenuClosedEvent { Name = page },
        Constants.NEW_GAME_PAGE => new NewGameMenuClosedEvent { Name = page },
        Constants.LOAD_GAME_PAGE => new LoadGameMenuClosedEvent { Name = page },
        Constants.DIFFICULTY_SELECTION_PAGE => new DifficultyMenuClosedEvent { Name = page },
        Constants.STARTING_CONDITIONS_PAGE => new StartConditionsMenuClosedEvent { Name = page },
        Constants.TUTORIALS_PAGE => new TutorialsMenuClosedEvent { Name = page },
        Constants.MULTIPLAYER_PAGE => new MultiplayerMenuClosedEvent { Name = page },
        Constants.WORKSHOP_PAGE => new WorkshopMenuClosedEvent { Name = page },
        Constants.SETTINGS_PAGE => new SettingsMenuClosedEvent { Name = page },
        _ => throw new NotImplementedException(),
      };
      this.OnEvent?.Invoke(pageEvent);
    }

    private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
      SceneLoadEvent specificEvent = scene.name switch
      {
        Constants.SPLASH_SCENE_NAME => new SplashSceneLoadEvent() { Scene = scene, LoadSceneMode = loadSceneMode },
        Constants.BASE_SCENE_NAME => new BaseSceneLoadEvent() { Scene = scene, LoadSceneMode = loadSceneMode },
        Constants.CHARACTER_CUSTOMIZATION_SCENE_NAME => new CharacterCutomizationSceneLoadEvent() { Scene = scene, LoadSceneMode = loadSceneMode },
        _ => throw new NotImplementedException(),
      };
      this.OnEvent?.Invoke(specificEvent);
    }

    private void SceneManager_sceneUnloaded(Scene scene)
    {
      SceneUnloadEvent specificEvent = scene.name switch
      {
        Constants.SPLASH_SCENE_NAME => new SplashSceneUnloadEvent() { Scene = scene },
        Constants.BASE_SCENE_NAME => new BaseSceneUnloadEvent() { Scene = scene },
        Constants.CHARACTER_CUSTOMIZATION_SCENE_NAME => new CharacterCutomizationSceneUnloadEvent() { Scene = scene },
        _ => throw new NotImplementedException(),
      };
      this.OnEvent?.Invoke(specificEvent);
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