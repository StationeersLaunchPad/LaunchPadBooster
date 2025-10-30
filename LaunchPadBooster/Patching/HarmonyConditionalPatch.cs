using System;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyConditionalPatch : Attribute
    {
        private Func<HarmonyConditionalPatch, object, bool> canPatch;
        private bool lastResult;

        public bool CanPatch(object o) => lastResult = canPatch(this, o);

        public virtual string Description => $"HarmonyConditionalPatch({lastResult})";

        public HarmonyConditionalPatch(Func<HarmonyConditionalPatch, object, bool> canPatch)
        {
            this.canPatch = canPatch;
        }
    }
}