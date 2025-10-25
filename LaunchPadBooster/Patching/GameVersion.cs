using System;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    class GameVersion : Attribute
    {
        public Version MinVersion;
        public Version MaxVersion;

        public bool VersionMatches(Version ver)
        {
            return ver >= this.MinVersion && ver <= this.MaxVersion;
        }
        public GameVersion(string minVersion, string maxVersion)
        {
            MinVersion = new Version(minVersion);
            MaxVersion = new Version(maxVersion);
        }
    }
}