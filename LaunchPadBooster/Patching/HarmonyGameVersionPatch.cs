using System;
using Assets.Scripts;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyGameVersionPatch : HarmonyConditionalPatch
    {
        public readonly Version MinVersion;
        public readonly Version MaxVersion;
        public static readonly Version CurrentVersion = typeof(GameManager).Assembly.GetName().Version;

        public override bool CanPatch => CurrentVersion >= this.MinVersion && CurrentVersion <= this.MaxVersion;
        public override string Description => $"Current: {CurrentVersion} Min: {this.MinVersion} Max: {this.MaxVersion}";

        public HarmonyGameVersionPatch(string minVersion, string maxVersion) 
        {
          this.MinVersion = new Version(minVersion);
          this.MaxVersion = new Version(maxVersion);
        }
    }
}