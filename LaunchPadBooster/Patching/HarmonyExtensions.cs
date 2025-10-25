using System.Diagnostics;
using System.Linq;
using Assets.Scripts;
using HarmonyLib;
using Debug = UnityEngine.Debug;

namespace LaunchPadBooster.Patching
{
    public static class HarmonyExtensions
    {
        public static void VersionAwarePatchAll(this Harmony harmony)
        {
            AccessTools.GetTypesFromAssembly(new StackTrace().GetFrame(1).GetMethod().ReflectedType?.Assembly).Do(type =>
            {
                var ver = type.GetCustomAttributes(true).OfType<GameVersion>().FirstOrDefault();
                var version = typeof(GameManager).Assembly.GetName().Version;
                
                if (ver != null && !ver.VersionMatches(version))
                {
                    Debug.Log($"Patch class {type.FullName} ignored because game version does not match!");
                    Debug.Log($"Current: {version} Min: {ver?.MinVersion} Max: {ver?.MaxVersion}");
                    return;
                }
                new VersionAwareClassProcessor(harmony, type, version).Patch();
            });
        }
    }
}