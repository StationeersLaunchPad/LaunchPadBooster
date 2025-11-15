using System;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyConditionalPatch : Attribute
    {
        public virtual bool CanPatch => true;

        public virtual string Description => $"HarmonyConditionalPatch()";
    }
}