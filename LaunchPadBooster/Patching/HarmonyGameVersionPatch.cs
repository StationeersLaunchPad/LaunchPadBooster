using System;
using Assets.Scripts;

namespace LaunchPadBooster.Patching;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class HarmonyGameVersionPatch(string minVersion, string maxVersion) : HarmonyConditionalPatch
{
  public readonly Version MinVersion = new(minVersion);
  public readonly Version MaxVersion = new(maxVersion);
  public static readonly Version CurrentVersion = typeof(GameManager).Assembly.GetName().Version;

  public override bool CanPatch => CurrentVersion >= MinVersion && CurrentVersion <= MaxVersion;
  public override string Description => $"Current: {CurrentVersion} Min: {MinVersion} Max: {MaxVersion}";
}