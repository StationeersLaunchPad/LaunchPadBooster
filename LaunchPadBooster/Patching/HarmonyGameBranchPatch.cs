using System;
using System.IO;
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

    private static string GetBetaBranchFromAcf()
    {
      var acfPath = Path.Combine(StationSaveUtils.ExeDirectory.FullName, "steamapps", "appmanifest_544550.acf");
      if (!File.Exists(acfPath)) return null;
      var acf = string.Join("\n", File.ReadAllLines(acfPath));
      var start = acf.IndexOf("\"UserConfig\"", StringComparison.Ordinal);
      start = acf.IndexOf("\"BetaKey\"", start, StringComparison.Ordinal);
      start = acf.IndexOf("\"", start+1, StringComparison.Ordinal);
      var end = acf.IndexOf("\n", start, StringComparison.Ordinal);
      if (start == -1 || end == -1) return null;
      var branch = acf.Substring(start, end - start).Replace("\"", "").Trim();
      return branch;
    }
    
    public static string CurrentBranch => _currentBranch ??= GetBetaBranchFromAcf() ?? Steamworks.SteamApps.CurrentBetaName ?? "public";

    public override bool CanPatch => this.Branches.Contains(CurrentBranch);
    public override string Description => $"Current: {CurrentBranch} Branches: [{string.Join(",", this.Branches)}]";

    public HarmonyGameBranchPatch(params string[] branches)
    {
      this.Branches = branches;
    }
  }
}