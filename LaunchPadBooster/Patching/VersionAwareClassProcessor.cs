using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LaunchPadBooster.Patching
{
    class VersionAwareClassProcessor : PatchClassProcessor
    {
        public VersionAwareClassProcessor(Harmony instance, Type type, Version version,
            bool allowUnannotatedType = false) : base(instance, type, allowUnannotatedType)
        {
            var patchMethods = (IList)typeof(PatchClassProcessor)
                .GetField("patchMethods", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(this);
            if (patchMethods == null || patchMethods.Count == 0)
            {
                return;
            }

            var attributePatchInfo = patchMethods[0].GetType()
                .GetField("info", BindingFlags.NonPublic | BindingFlags.Instance);

            var toRemove = new List<object>();
            if (attributePatchInfo == null)
            {
                return;
            }

            foreach (var patchMethod in patchMethods)
            {
                var info = (HarmonyMethod)attributePatchInfo.GetValue(patchMethod);
                HarmonyVersionPatch ver = info.method.GetCustomAttributes().OfType<HarmonyVersionPatch>().FirstOrDefault();
                if (ver != null && !ver.CanPatch(version))
                {
                    Debug.Log(
                        $"Patch in {type.FullName}.{info.method.Name} for {info.declaringType.Name}.{info.methodName} ignored because game version does not match!");
                    Debug.Log($"Current: {version} Min: {ver?.MinVersion} Max: {ver?.MaxVersion}");
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