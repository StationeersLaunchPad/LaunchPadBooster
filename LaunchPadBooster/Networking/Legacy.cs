using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Assets.Scripts.Networking;
using HarmonyLib;
using UnityEngine.Networking;

namespace LaunchPadBooster.Networking;

public interface IModNetworkMessage;

public abstract class ModNetworkMessage<T> : ProcessedMessage<T>, IModNetworkMessage
  where T : ModNetworkMessage<T>, new();

internal class UnknownLegacyMessage : ModNetworkMessage<UnknownLegacyMessage>
{
  public override void Deserialize(RocketBinaryReader reader) { }
  public override void Serialize(RocketBinaryWriter writer) { }
}

internal partial class ModNetworking
{
  internal static readonly TypeRegistry<IModNetworkMessage> legacyRegistry = new();

  internal static void RegisterLegacyMessage<T>(Mod mod) where T : ModNetworkMessage<T>, new() =>
    legacyRegistry.RegisterType<T>(mod);

  internal static void WriteLegacyMessageType(RocketBinaryWriter writer, Type type)
  {
    if (typeof(IModNetworkMessage).IsAssignableFrom(type))
    {
      writer.WriteByte(255);
      var typeID = legacyRegistry.TypeIDFor(type);
      writer.WriteInt32(typeID.ModHash);
      writer.WriteInt32(typeID.TypeHash);
    }
    else
      writer.WriteByte(MessageFactory.GetIndexFromType(type));
  }

  internal static Type ReadLegacyMessageType(RocketBinaryReader reader)
  {
    var index = reader.ReadByte();
    if (index == 255)
    {
      var modHash = reader.ReadInt32();
      var typeHash = reader.ReadInt32();
      if (legacyRegistry.TypeFor(new(modHash, typeHash), out var type))
        return type;
      return typeof(UnknownLegacyMessage);
    }

    return MessageFactory.GetTypeFromIndex(index);
  }

  private static partial class Patches
  {
    [HarmonyPatch(typeof(RocketBinaryReader), nameof(RocketBinaryReader.ReadMessageType)), HarmonyPrefix]
    static bool RocketBinaryReader_ReadMessageType(RocketBinaryReader __instance, ref Type __result)
    {
      __result = ReadLegacyMessageType(__instance);
      return false;
    }

    [HarmonyPatch(typeof(RocketBinaryWriter), nameof(RocketBinaryWriter.WriteMessageType)), HarmonyPrefix]
    static bool RocketBinaryWriter_WriteMessageType(RocketBinaryWriter __instance, Type value)
    {
      WriteLegacyMessageType(__instance, value);
      return false;
    }

    [HarmonyPatch(typeof(NetworkBase), nameof(NetworkBase.DeserializeReceivedData)), HarmonyTranspiler]
    static IEnumerable<CodeInstruction> NetworkBase_DeserializeReceivedData(
      IEnumerable<CodeInstruction> instructions)
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
