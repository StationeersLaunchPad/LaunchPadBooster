
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Networking;
using HarmonyLib;

namespace LaunchPadBooster.Networking
{
  static class ModNetworkPatches
  {
    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayerRequest), nameof(NetworkMessages.VerifyPlayerRequest.Serialize)), HarmonyPostfix]
    static void VerifyPlayerRequest_Serialize(RocketBinaryWriter writer)
    {
      ModNetworking.SerializeServerModInfo(writer);
    }

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayerRequest), nameof(NetworkMessages.VerifyPlayerRequest.Deserialize)), HarmonyPostfix]
    static void VerifyPlayerRequest_Deserialize(RocketBinaryReader reader)
    {
      ModNetworking.DeserializeServerModInfo(reader);
    }

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayerRequest), nameof(NetworkMessages.VerifyPlayerRequest.Process)), HarmonyPrefix]
    static bool VerifyPlayerRequest_Process(long hostId)
    {
      return !NetworkManager.IsClient || ModNetworking.ValidateModInfoClient();
    }

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayer), nameof(NetworkMessages.VerifyPlayer.Serialize)), HarmonyPostfix]
    static void VerifyPlayer_Serialize(RocketBinaryWriter writer)
    {
      ModNetworking.SerializeClientModInfo(writer);
    }

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayer), nameof(NetworkMessages.VerifyPlayer.Deserialize)), HarmonyPostfix]
    static void VerifyPlayer_Deserialize(RocketBinaryReader reader)
    {
      ModNetworking.DeserializeClientModInfo(reader);
    }

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayer), nameof(NetworkMessages.VerifyPlayer.Process)), HarmonyPrefix]
    static bool VerifyPlayer_Process(long hostId, NetworkMessages.VerifyPlayer __instance)
    {
      return !NetworkManager.IsServer || ModNetworking.ValidateModInfoServer(hostId, __instance);
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.GetFallbackInterface), typeof(int)), HarmonyPrefix]
    static bool Localization_GetFallbackInterface(int hash, ref string __result)
    {
      // this is used by the error popup dialog, but often receives text strings instead of localization keys
      // properly return empty string when not found here so the original string is used

      // fallback is not changed anywhere so its always english
      const LanguageCode fallbackLanguage = LanguageCode.EN;
      if (Localization.CurrentLanguage == fallbackLanguage && !Localization.InterfaceExists(hash))
      {
        __result = string.Empty;
        return false;
      }
      return true;
    }

    [HarmonyPatch(typeof(RocketBinaryReader), nameof(RocketBinaryReader.ReadMessageType)), HarmonyPrefix]
    static bool RocketBinaryReader_ReadMessageType(RocketBinaryReader __instance, ref Type __result)
    {
      __result = ModNetworking.ReadMessageType(__instance);
      return false;
    }

    [HarmonyPatch(typeof(RocketBinaryWriter), nameof(RocketBinaryWriter.WriteMessageType)), HarmonyPrefix]
    static bool RocketBinaryWriter_WriteMessageType(RocketBinaryWriter __instance, Type value)
    {
      ModNetworking.WriteMessageType(__instance, value);
      return false;
    }

    [HarmonyPatch(typeof(NetworkBase), nameof(NetworkBase.DeserializeReceivedData)), HarmonyTranspiler]
    static IEnumerable<CodeInstruction> NetworkBase_DeserializeReceivedData(IEnumerable<CodeInstruction> instructions)
    {
      /*
      The network client checks message types against a whitelist for messages the client is allowed to send.
      Each case in the switch statement looks like:

      ldloc.1
      isinst <type>
      brtrue.s <label>

      We find the first isinst check and look at the instructions before and after to get the load and jump targets
      */

      var matcher = new CodeMatcher(instructions);
      matcher.MatchStartForward(new CodeMatch(OpCodes.Isinst, typeof(NetworkMessages.Handshake)));
      matcher.ThrowIfInvalid("Could not find insertion point for NetworkBase.DeserializeReceivedData");

      // get ldloc instruction
      matcher.Advance(-1);
      var ldinst = matcher.Instruction;

      // get jump instruction
      matcher.Advance(2);
      var jumpinst = matcher.Instruction;

      // insert our case after the handshake case
      matcher.Advance(1);
      matcher.InsertAndAdvance(new CodeInstruction(ldinst));
      matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Isinst, typeof(IModNetworkMessage)));
      matcher.InsertAndAdvance(new CodeInstruction(jumpinst));

      return matcher.Instructions();
    }
  }
}