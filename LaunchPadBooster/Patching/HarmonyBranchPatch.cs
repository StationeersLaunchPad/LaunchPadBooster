using System;
using System.Linq;

namespace LaunchPadBooster.Patching
{
    // TODO: Disable until a way to get the current branch is determined
    // [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyBranchPatch : HarmonyConditionalPatch
    {
        public readonly String[] Branches;
        public static readonly String CurrentBranch = "public"; 
        public HarmonyBranchPatch(String[] branches) : 
            base((h) => ((HarmonyBranchPatch)h).Branches.Contains(CurrentBranch))
        {
            Branches = branches;
        }
    }
}