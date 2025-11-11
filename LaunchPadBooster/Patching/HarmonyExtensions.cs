using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Debug = UnityEngine.Debug;

namespace LaunchPadBooster.Patching
{
    public static class HarmonyExtensions
    {
        public static void ConditionalPatchAll(this Harmony harmony)
        {
            AccessTools.GetTypesFromAssembly(new StackTrace().GetFrame(1).GetMethod().ReflectedType?.Assembly).Do(type =>
            {
                var patch = type.GetCustomAttributes(true).OfType<HarmonyConditionalPatch>().FirstOrDefault();
                
                if (patch != null && !patch.CanPatch())
                {
                    Debug.Log($"Patch class {type.FullName} ignored because specified condition is false!");
                    Debug.Log(patch.Description);
                    return;
                }
                new HarmonyConditionalClassProcessor(harmony, type).Patch();
            });
        }
    }
}