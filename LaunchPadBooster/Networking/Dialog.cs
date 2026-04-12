
using System;
using Assets.Scripts;
using Assets.Scripts.Networking;
using LaunchPadBooster.Utils;
using UI;
using UnityEngine;
using UnityEngine.Events;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  private delegate void ConfirmationPanelDelegate(
    ConfirmationPanel instance,
    string title,
    string message,
    string button1Text,
    UnityAction button1Click
  );
  private static ConfirmationPanelDelegate ShowConfirmationPanel;

  internal static void InitializeConfirmationPanel()
  {
    // make ConfirmationPanel delegate manually for stable/beta compatibility
    try
    {
      if (CompatUtils.TryMakeDelegate<ConfirmationPanelDelegate>(
          typeof(ConfirmationPanel), "SetUpPanel", out var m1))
      {
        // use as-is
        ShowConfirmationPanel = m1;
      }
      else if (CompatUtils.TryMakeDelegate<ConfirmationPanelDelegate>(
        typeof(ConfirmationPanel), "ShowRaw", out var m2))
      {
        // localize b1text
        ShowConfirmationPanel = (instance, title, message, b1text, b1click) =>
          m2(instance, title, message, Localization.GetInterface(b1text), b1click);
      }
    }
    catch (Exception ex)
    {
      Debug.LogException(ex);
    }
    // fallback to printing to console if neither found
    ShowConfirmationPanel ??= (_, title, message, _, click) =>
      ConsoleWindow.PrintError($"{title}: {message}", true);
  }

  internal static void FailClientJoin(string message)
  {
    ConsoleWindow.PrintError(message, true);
    NetworkClient.StopConnectionTimer();
    NetworkManager.EndConnection();
    ShowConfirmationPanel(
      ConfirmationPanel.Instance, "Incompatible mods", message, "ButtonOk", NetworkClient.Cancel);
  }
}