using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LaunchPadBooster.Patching
{
    class HarmonyConditionalClassProcessor : PatchClassProcessor
    {
        public HarmonyConditionalClassProcessor(Harmony instance, Type type,
            bool allowUnannotatedType = false) : base(instance, type, allowUnannotatedType)
        {
            var patchMethods = (IList)typeof(PatchClassProcessor)
                .GetField("patchMethods", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(this);
            
            if (patchMethods == null || patchMethods.Count == 0) return;

            var attributePatchInfo = patchMethods[0].GetType()
                .GetField("info", BindingFlags.NonPublic | BindingFlags.Instance);

            var toRemove = new List<object>();
            if (attributePatchInfo == null) return;

            foreach (var patchMethod in patchMethods)
            {
                var info = (HarmonyMethod)attributePatchInfo.GetValue(patchMethod);
                var patches = info.method.GetCustomAttributes().OfType<HarmonyConditionalPatch>().ToList();
                foreach (var patch in patches.Where(patch => !patch.CanPatch))
                {
                  if (info.debug ?? false)
                  {
                    Debug.Log(
                      $"Patch in {type.FullName}.{info.method.Name} for {info.declaringType.Name}.{info.methodName} ignored because specified condition is false!");
                    Debug.Log(patch.Description);
                  }
                  toRemove.Add(patchMethod);
                }
            }

            foreach (var patchMethod in toRemove)
            {
                patchMethods.Remove(patchMethod);
            }
        }
    }
}