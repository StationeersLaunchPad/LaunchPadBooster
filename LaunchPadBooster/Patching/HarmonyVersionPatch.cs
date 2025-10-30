using System;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    class HarmonyVersionPatch : HarmonyConditionalPatch
    {
        public readonly Version MinVersion;
        public readonly Version MaxVersion;
        
        public HarmonyVersionPatch(string minVersion, string maxVersion) 
            : base((h,ver) 
                => (Version) ver >= ((HarmonyVersionPatch) h).MinVersion && (Version) ver <= ((HarmonyVersionPatch) h).MaxVersion)
        {
            MinVersion = new Version(minVersion);
            MaxVersion = new Version(maxVersion);
        }
    }
}