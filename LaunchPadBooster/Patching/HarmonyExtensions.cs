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
                var patch = type.GetCustomAttributes(true).OfType<HarmonyConditionalPatch>().FirstOrDefault();
                var version = typeof(GameManager).Assembly.GetName().Version;
                
                if (patch != null && !patch.CanPatch(version))
                {
                    Debug.Log($"Patch class {type.FullName} ignored because game version does not match!");
                    Debug.Log($"Current: {version} {patch.Description}");
                    return;
                }
                new VersionAwareClassProcessor(harmony, type, version).Patch();
            });
        }
    }
}