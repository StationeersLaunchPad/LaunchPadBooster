using HarmonyLib;
using UnityEngine.SceneManagement;

namespace LaunchPadBooster.Events
{
  public static class ModEvents
  {
    private static readonly object initLock = new();
    private static bool initialized;

    internal static void Initialize()
    {
      lock (initLock)
      {
        if (initialized)
          return;

        var harmony = new Harmony("LaunchPadBooster.Events");
        harmony.CreateClassProcessor(typeof(ModEventPatches), true).Patch();

        initialized = true;
      }
    }
  }

  // Base Scene Events

  public class SceneEvent : ModEvent
  {
    public Scene Scene;
    public string Name => Scene.name;
    public string Path => Scene.path;

    public bool IsLoaded => Scene.isLoaded;
    public bool IsSubscene => Scene.isSubScene;
    public bool IsValid => Scene.IsValid();
  }

  public class SceneLoadEvent : SceneEvent
  {
    public LoadSceneMode LoadSceneMode;
    public bool IsSingle => LoadSceneMode == LoadSceneMode.Single;
    public bool IsAdditive => LoadSceneMode == LoadSceneMode.Additive;

    public void UnloadAsync() => SceneManager.UnloadSceneAsync(Scene);
  }

  public class SceneUnloadEvent : SceneEvent {}

  // Specific Scene Events

  public class SplashSceneLoadEvent : SceneLoadEvent { }
  public class SplashSceneUnloadEvent : SceneUnloadEvent { }

  public class BaseSceneLoadEvent : SceneLoadEvent { }
  public class BaseSceneUnloadEvent : SceneUnloadEvent { }

  public class CharacterCutomizationSceneLoadEvent : SceneLoadEvent { }
  public class CharacterCutomizationSceneUnloadEvent : SceneUnloadEvent { }


  // Base Menu Page Events

  public class MenuPageEvent : ModEvent
  {
    public string Name;
    public virtual bool Open => false;
  }

  public class MenuPageOpenedEvent : MenuPageEvent
  {
    public override bool Open => true;
  }

  public class MenuPageClosedEvent : MenuPageEvent
  {
    public override bool Open => false;
  }

  // Menu Page Events

  public class MainMenuOpenedEvent : MenuPageOpenedEvent {}
  public class MainMenuClosedEvent : MenuPageClosedEvent {}

  public class NewGameMenuOpenedEvent : MenuPageOpenedEvent {}
  public class NewGameMenuClosedEvent : MenuPageClosedEvent {}

  public class LoadGameMenuOpenedEvent : MenuPageOpenedEvent {}
  public class LoadGameMenuClosedEvent : MenuPageClosedEvent {}

  public class DifficultyMenuOpenedEvent : MenuPageOpenedEvent {}
  public class DifficultyMenuClosedEvent : MenuPageClosedEvent {}

  public class StartConditionsMenuOpenedEvent : MenuPageOpenedEvent {}
  public class StartConditionsMenuClosedEvent : MenuPageClosedEvent {}

  public class TutorialsMenuOpenedEvent : MenuPageOpenedEvent {}
  public class TutorialsMenuClosedEvent : MenuPageClosedEvent {}

  public class MultiplayerMenuOpenedEvent : MenuPageOpenedEvent {}
  public class MultiplayerMenuClosedEvent : MenuPageClosedEvent {}

  public class WorkshopMenuOpenedEvent : MenuPageOpenedEvent {}
  public class WorkshopMenuClosedEvent : MenuPageClosedEvent {}

  public class SettingsMenuOpenedEvent : MenuPageOpenedEvent {}
  public class SettingsMenuClosedEvent : MenuPageClosedEvent {}
}