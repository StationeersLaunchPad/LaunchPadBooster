using System;
using System.Linq;

namespace LaunchPadBooster.Patching
{
    // TODO: Disable until a way to get the current branch is determined
    // [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyGameBranchPatch : HarmonyConditionalPatch
    {
        public readonly String[] Branches;
        public static readonly String CurrentBranch = "public";
        
        public override string Description => $"Current: {CurrentBranch} Branches: [{string.Join(",", Branches)}]";
        public HarmonyGameBranchPatch(String[] branches) : 
            base((h) => ((HarmonyGameBranchPatch)h).Branches.Contains(CurrentBranch))
        {
            Branches = branches;
        }
    }
}