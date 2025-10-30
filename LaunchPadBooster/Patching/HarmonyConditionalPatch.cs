using System;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyConditionalPatch : Attribute
    {
        private Func<HarmonyConditionalPatch, object, bool> canPatch;

        public bool CanPatch(object o) => canPatch(this, o);
        
        public HarmonyConditionalPatch(Func<HarmonyConditionalPatch, object, bool> canPatch)
        {
            this.canPatch = canPatch;
        }
    }
}