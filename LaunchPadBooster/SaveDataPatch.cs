using System;
using System.Collections.Generic;
using Assets.Scripts.Serialization;
using HarmonyLib;
using LaunchPadBooster.Utils;

namespace LaunchPadBooster;

internal static class SaveDataPatch
{
  static readonly object initLock = new();
  static bool initialized = false;

  public static void Initialize()
  {
    lock (initLock)
    {
      if (initialized)
        return;

      var harmony = new Harmony("LaunchPadBooster.SaveDataPatch");
      List<Type> dummyParam = null;
      harmony.Patch(
        ReflectionUtils.Method(() => XmlSaveLoad.AddExtraTypes(ref dummyParam)),
        prefix: new HarmonyMethod(ReflectionUtils.Method(() => PatchSaveData(ref dummyParam)))
      );

      initialized = true;
    }
  }

  [HarmonyPrefix]
  private static void PatchSaveData(ref List<Type> extraTypes)
  {
    foreach (var mod in Mod.AllMods)
      extraTypes.AddRange(mod.SaveDataTypes);
  }
}