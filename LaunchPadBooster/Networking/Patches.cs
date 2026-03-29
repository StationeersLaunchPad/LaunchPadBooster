
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Networking;
using DLC;
using HarmonyLib;
using LaunchPadBooster.Utils;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  internal static readonly object initLock = new();
  internal static bool initialized = false;

  internal static void Initialize()
  {
    lock (initLock)
    {
      if (initialized)
        return;

      InitializeConfirmationPanel();

      var harmony = new Harmony("LaunchPadBooster.NetworkingV2");
      harmony.CreateClassProcessor(typeof(Patches), true).Patch();
      foreach (var subtype in typeof(Patches).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        harmony.CreateClassProcessor(subtype, true).Patch();
      initialized = true;
    }
  }

  internal static byte[] NetworkManagerBuffer => field ??=
    (byte[])typeof(NetworkManager).GetField(
      "Buffer", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

  internal static bool ReceiveUnknownEvent(int channel, int size, long connectionId)
  {
    if (channel != (int)BoosterNetworkChannel)
      throw new ArgumentOutOfRangeException($"{channel}");

    ReceivePacket(connectionId, NetworkManagerBuffer.AsSpan(0, size));

    return true;
  }

  private static partial class Patches
  {
    // Replace throw ArgumentOutOfRangeException on unmatched NetworkChannel
    [HarmonyPatch(typeof(NetworkManager), "ReceiveEvents"), HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> TranspileReceiveEvents(
      IEnumerable<CodeInstruction> instructions)
    {
      var matcher = new CodeMatcher(instructions);

      matcher.MatchStartForward(
        new CodeMatch(inst =>
          inst.opcode == OpCodes.Call &&
          inst.operand is MethodInfo { Name: "HandleGeneralTraffic" }));
      matcher.ThrowIfInvalid("Could not find HandleGeneralTraffic call in NetworkManager.ReceiveEvents");

      matcher.MatchStartForward(
        new CodeMatch(inst =>
          inst.opcode == OpCodes.Newobj &&
          inst.operand is ConstructorInfo ctor &&
          ctor.DeclaringType == typeof(ArgumentOutOfRangeException)),
        new CodeMatch(OpCodes.Throw));
      matcher.ThrowIfInvalid(
        "Could not find throw ArgumentOutOfRangeException in NetworkManager.ReceiveEvents");

      var labels = matcher.Instruction.labels;

      matcher.RemoveInstructions(2);
      matcher.InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_1) { labels = labels }, // channel
        new CodeInstruction(OpCodes.Ldloc_2), // size
        new CodeInstruction(OpCodes.Ldloc_3), // connectionId
        CodeInstruction.Call(() => ReceiveUnknownEvent(default, default, default)),
        new CodeInstruction(OpCodes.Ret)
      );

      return matcher.Instructions();
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.ManagerUpdate)), HarmonyPostfix]
    internal static void RunCleanup() => Cleanup();

    [HarmonyPatch(typeof(NetworkManager), "PlayerConnected"), HarmonyPostfix]
    internal static void PlayerConnected(long connectionId, ConnectionMethod connectionMethod) =>
      AddConnection(connectionId, connectionMethod);

    [HarmonyPatch(typeof(NetworkManager), "PlayerDisconnected"), HarmonyPostfix]
    internal static void PlayerDisconnected(long connectionId) => RemoveConnection(connectionId);

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.EndConnection)), HarmonyPostfix]
    internal static void EndConnection() => ClearConnections();

    [HarmonyPatch(
      typeof(NetworkMessages.VerifyPlayerRequest), nameof(ProcessedMessage<>.Serialize)), HarmonyPostfix]
    internal static void WriteJoinHeaderServer(RocketBinaryWriter writer) => WriteJoinValidateHeader(writer);

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayer), nameof(ProcessedMessage<>.Serialize)), HarmonyPostfix]
    internal static void WriteJoinHeaderClient(RocketBinaryWriter writer) => WriteJoinValidateHeader(writer);

    [HarmonyPatch(
      typeof(NetworkMessages.VerifyPlayerRequest), nameof(ProcessedMessage<>.Deserialize)), HarmonyPostfix]
    internal static void ReadJoinHeaderClient(RocketBinaryReader reader) =>
      ReadJoinValidateHeader(GetHostId(), reader);

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayer), nameof(ProcessedMessage<>.Deserialize)), HarmonyPostfix]
    internal static void ReadJoinHeaderServer(
      NetworkMessages.VerifyPlayer __instance, RocketBinaryReader reader
    ) => ReadJoinValidateHeader(__instance.OwnerConnectionId, reader);

    [HarmonyPatch(typeof(NetworkMessages.VerifyPlayer), nameof(ProcessedMessage<>.Process)), HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> TranspileVerifyPlayer(IEnumerable<CodeInstruction> instructions)
    {
      var matcher = new CodeMatcher(instructions);

      matcher.MatchStartForward(CodeInstruction.Call(() => NetworkServer.VerifyConnection(default, default)));
      matcher.ThrowIfInvalid("Could not find NetworkServer.VerifyConnection call in VerifyPlayer.Process");
      matcher.Instruction.operand = ReflectionUtils.Method(() => ReceiveVerifyPlayer(default, default));

      return matcher.Instructions();
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

    [HarmonyPatch(typeof(NetworkServer), "PackageJoinData"), HarmonyPrefix]
    static void NetworkServer_PackageJoinData(RocketBinaryWriter ____joinWriter)
    {
      WriteJoinPrefix(____joinWriter);
    }

    [HarmonyPatch(typeof(AtmosphericsManager), nameof(AtmosphericsManager.SerialiseOnJoin)), HarmonyPostfix]
    static void AtmosphericsManager_SerializeOnJoin(RocketBinaryWriter writer)
    {
      WriteJoinSuffix(writer);
    }

    [HarmonyPatch]
    static class ProcessJoinDataPatch
    {
      [HarmonyTargetMethod]
      static MethodBase TargetMethod()
      {
        var asyncMethod = typeof(NetworkClient).GetMethod(
          "ProcessJoinData", BindingFlags.Static | BindingFlags.NonPublic);
        var smType = asyncMethod.GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType;
        return smType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
      }

      [HarmonyTranspiler]
      static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
      {
        var matcher = new CodeMatcher(instructions);

        matcher.Start();
        matcher.MatchStartForward(CodeInstruction.Call(() => GameManager.DeserializeGameTime(default)));
        matcher.ThrowIfInvalid(
          "Failed to find GameManager.DeserializeGameTime in NetworkClient.ProcessJoinData");
        matcher.Insert(
          new CodeInstruction(OpCodes.Dup), // copy reader
          CodeInstruction.Call(() => ReadJoinPrefix(default))
        );

        matcher.Start();
        matcher.MatchStartForward(CodeInstruction.Call(() => AtmosphericsManager.DeserializeOnJoin(default)));
        matcher.ThrowIfInvalid(
          "Failed to find AtmosphericsManager.DeserializeOnJoin in NetworkClient.ProcessJoinData");
        matcher.Instruction.operand = ReflectionUtils.Method(() => ReadJoinSuffix(default));

        return matcher.Instructions();
      }
    }

    [HarmonyPatch(typeof(FragmentHandler), "WriteStateImmediate"), HarmonyPrefix]
    static void FragmentHandler_WriteStateImmediate_Prefix(RocketBinaryWriter writer)
    {
      WriteUpdatePrefix(writer);
    }

    [HarmonyPatch(typeof(SharedDLCManager), nameof(SharedDLCManager.SerializeDeltaState)), HarmonyPostfix]
    static void SharedDLCManager_SerializeDeltaState_Postfix(RocketBinaryWriter writer)
    {
      WriteUpdateSuffix(writer);
    }

    [HarmonyPatch(typeof(FragmentHandler), "ReadStateImmediate"), HarmonyPrefix]
    static void FragmentHandler_ReadStateImmediate_Prefix(RocketBinaryReader reader)
    {
      ReadUpdatePrefix(reader);
    }

    [HarmonyPatch(typeof(SharedDLCManager), nameof(SharedDLCManager.DeserializeDeltaState)), HarmonyPostfix]
    static void SharedDLCManager_DeserializeDeltaState_Postfix(RocketBinaryReader reader)
    {
      ReadUpdateSuffix(reader);
    }
  }
}