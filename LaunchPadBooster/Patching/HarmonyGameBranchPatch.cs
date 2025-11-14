using System;
using System.Linq;

namespace LaunchPadBooster.Patching
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HarmonyGameBranchPatch : HarmonyConditionalPatch
    {
        public readonly string[] Branches;
        
        //Note: the reason for using `public` for the case where the branch is not specified is
        //because when downloading a branch with steamcmd, when you specify `-beta public`, it will download the main branch

        private static string _currentBranch;
        public static string CurrentBranch => _currentBranch ??= Steamworks.SteamApps.CurrentBetaName ?? "public";

        public override string Description => $"Current: {CurrentBranch} Branches: [{string.Join(",", this.Branches)}]";
        public HarmonyGameBranchPatch(params string[] branches) : 
            base((h) => ((HarmonyGameBranchPatch)h).Branches.Contains(CurrentBranch)) =>
          this.Branches = branches;
    }
}