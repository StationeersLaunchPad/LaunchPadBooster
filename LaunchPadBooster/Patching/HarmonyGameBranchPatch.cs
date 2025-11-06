using System;
using System.Linq;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyGameBranchPatch : HarmonyConditionalPatch
    {
        public readonly String[] Branches;
        
        //Note: the reason for using `public` for the case where the branch is not specified is
        //because when downloading a branch with steamcmd, when you specify `-beta public`, it will download the main branch
        public static readonly String CurrentBranch = Steamworks.SteamApps.CurrentBetaName ?? "public";
        
        public override string Description => $"Current: {CurrentBranch} Branches: [{string.Join(",", Branches)}]";
        public HarmonyGameBranchPatch(String[] branches) : 
            base((h) => ((HarmonyGameBranchPatch)h).Branches.Contains(CurrentBranch))
        {
            Branches = branches;
        }
    }
}