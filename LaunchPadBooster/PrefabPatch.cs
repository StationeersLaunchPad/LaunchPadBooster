using Assets.Scripts.Objects;
using HarmonyLib;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace LaunchPadBooster;

internal static class PrefabPatch
{
  static readonly object initLock = new();
  static bool initialized = false;

  public static void Initialize()
  {
    lock (initLock)
    {
      if (initialized)
        return;

      var harmony = new Harmony("LaunchPadBooster.PrefabPatch");
      harmony.Patch(
        ReflectionUtils.Method(() => Prefab.LoadAll()),
        prefix: new HarmonyMethod(ReflectionUtils.Method(() => PatchPrefabs()))
      );
      initialized = true;
    }
  }

  [HarmonyPrefix]
  private static void PatchPrefabs()
  {
    // add all prefabs to worldmanager first so setup can find other mods prefabs
    foreach (var mod in Mod.AllMods)
    {
      foreach (var prefab in mod.Prefabs)
      {
        if (!WorldManager.Instance.SourcePrefabs.Contains(prefab))
        {
          WorldManager.Instance.SourcePrefabs.Add(prefab);
          Debug.Log($"Add prefab to WorldManager: {prefab.name}");
        }
        else
        {
          Debug.Log($"Already contains prefab: {prefab.name}");
        }
      }
    }

    foreach (var mod in Mod.AllMods)
      foreach (var setup in mod.Setups)
        setup.Run(mod.Prefabs);
  }
}