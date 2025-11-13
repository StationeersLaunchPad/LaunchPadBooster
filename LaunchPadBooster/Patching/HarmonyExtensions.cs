using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Debug = UnityEngine.Debug;

namespace LaunchPadBooster.Patching
{
    public static class HarmonyExtensions
    {
        /** Runs a conditional patch on all types in the calling assembly.
         * Note: Will run a patch on all classes in the assembly.
         * If you wish to patch on a specific class,
         * use <see cref="ConditionalPatchAll(Harmony, Type)"/>
         * or <see cref="ConditionalPatch(Harmony, Type)"/> 
         * <param name="harmony">Harmony Object</param>
         */
        public static void ConditionalPatchAll(this Harmony harmony)
        {
            AccessTools.GetTypesFromAssembly(new StackTrace().GetFrame(1).GetMethod().ReflectedType?.Assembly).Do(type => ConditionalPatch(harmony, type));
        }

        /** Runs a recursive conditional patch on all types nested in the given type
         * Inclusive of the parent type
         * <param name="harmony">Harmony Object</param>
         * <param name="type">Specified type</param>
         */
        public static void ConditionalPatchAll(this Harmony harmony, Type type)
        {
            type.GetNestedTypes().Do(nType => ConditionalPatchAll(harmony, nType));
            ConditionalPatch(harmony, type);
        }
        
        /** Runs a conditional patch on the specific type referenced.
         *  Notice: Does not apply patches in child classes.
         *  Use <see cref="ConditionalPatchAll(Harmony, Type)"/> for this use case
         * <param name="harmony">Harmony Object</param>
         * <param name="type">Specified type</param>
         */
        public static void ConditionalPatch(this Harmony harmony, Type type)
        {
            var patch = type.GetCustomAttributes(true).OfType<HarmonyConditionalPatch>().FirstOrDefault();
                
            if (patch != null && !patch.CanPatch())
            {
                Debug.Log($"Patch class {type.FullName} ignored because specified condition is false!");
                Debug.Log(patch.Description);
                return;
            }
            new HarmonyConditionalClassProcessor(harmony, type).Patch();
        }
    }
}