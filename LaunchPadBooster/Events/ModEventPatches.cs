using HarmonyLib;
using System;

namespace LaunchPadBooster.Events
{
  static class ModEventPatches
  {
    public static event Action<string> OnMenuPageEnabled;
    public static event Action<string> OnMenuPageDisabled;

    [HarmonyPatch(typeof(MainMenuWindowManager), nameof(MainMenuWindowManager.EnableMainMenuPage)), HarmonyPostfix]
    public static void MainMenuWindowManager_EnableMainMenuPage(ref MainMenuWindowManager __instance, string windowName)
    {
      OnMenuPageEnabled?.Invoke(windowName);
    }

    [HarmonyPatch(typeof(MainMenuWindowManager), nameof(MainMenuWindowManager.DisableMainMenuPage)), HarmonyPostfix]
    public static void MainMenuWindowManager_DisableMainMenuPage(ref MainMenuWindowManager __instance, string windowName)
    {
      OnMenuPageDisabled?.Invoke(windowName);
    }
  }
}
