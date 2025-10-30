using System;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyConditionalPatch : Attribute
    {
        private Func<HarmonyConditionalPatch, bool> canPatch;
        private bool lastResult;

        public bool CanPatch() => lastResult = canPatch(this);

        public virtual string Description => $"HarmonyConditionalPatch({lastResult})";

        public HarmonyConditionalPatch(Func<HarmonyConditionalPatch, bool> canPatch)
        {
            this.canPatch = canPatch;
        }
    }
}