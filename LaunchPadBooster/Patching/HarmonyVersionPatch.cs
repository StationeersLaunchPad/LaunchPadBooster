using System;
using Assets.Scripts;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    class HarmonyVersionPatch : HarmonyConditionalPatch
    {
        public readonly Version MinVersion;
        public readonly Version MaxVersion;
        public static readonly Version CurrentVersion = typeof(GameManager).Assembly.GetName().Version;

        public override string Description => $"Current: {CurrentVersion} Min: {MinVersion} Max: {MaxVersion}";

        public HarmonyVersionPatch(string minVersion, string maxVersion) 
            : base((h) 
                => CurrentVersion >= ((HarmonyVersionPatch) h).MinVersion && CurrentVersion <= ((HarmonyVersionPatch) h).MaxVersion)
        {
            MinVersion = new Version(minVersion);
            MaxVersion = new Version(maxVersion);
        }
    }
}