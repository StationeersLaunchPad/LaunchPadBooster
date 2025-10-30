using System;
using System.Linq;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyBranchPatch : HarmonyConditionalPatch
    {
        public readonly String[] Branches;
        public HarmonyBranchPatch(String[] branches) : 
            base((h, o) => ((HarmonyBranchPatch)h).Branches.Contains(o))
        {
            Branches = branches;
        }
    }
}