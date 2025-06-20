using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Networking;
using HarmonyLib;
using UnityEngine.Networking;

namespace LaunchPadBooster.Networking
{
  public interface IModNetworkMessage { }

  public abstract class ModNetworkMessage<T> : ProcessedMessage<T>, IModNetworkMessage where T : ModNetworkMessage<T>, new()
  {
    public static void Register()
    {
      ModNetworkMessages.Register<T>();
    }
  }

  public static class ModNetworkMessages
  {
    private static readonly object initLock = new();
    private static bool initialized;

    private static readonly List<Type> MessageTypes = new();
    private static readonly Dictionary<Type, ushort> TypeToIndex = new();

    private static void Initialize()
    {
      lock (initLock)
      {
        if (initialized)
          return;

        var harmony = new Harmony("LaunchPadBooster.Networking");
        harmony.CreateClassProcessor(typeof(ModNetworkPatches), true).Patch();

        initialized = true;
      }
    }

    public static void Register<T>() where T : ModNetworkMessage<T>, new()
    {
      Initialize();
      var type = typeof(T);
      if (TypeToIndex.ContainsKey(type)) return;
      var index = (ushort)MessageTypes.Count;
      MessageTypes.Add(type);
      TypeToIndex[type] = index;
    }

    public static void WriteMessageType(RocketBinaryWriter writer, Type type)
    {
      if (typeof(IModNetworkMessage).IsAssignableFrom(type))
      {
        writer.WriteByte(255);
        writer.WriteUInt16(TypeToIndex[type]);
      }
      else
      {
        writer.WriteByte(MessageFactory.GetIndexFromType(type));
      }
    }

    public static Type ReadMessageType(RocketBinaryReader reader)
    {
      var index = reader.ReadByte();
      if (index == 255)
      {
        var mindex = reader.ReadUInt16();
        return MessageTypes[mindex];
      }
      else
      {
        return MessageFactory.GetTypeFromIndex(index);
      }
    }

    public static void WriteMessageTypesOnJoin(RocketBinaryWriter writer)
    {
      writer.WriteUInt16((ushort)MessageTypes.Count);
      foreach (var type in MessageTypes)
      {
        writer.WriteString(type.FullName);
      }
    }

    public static void ReadMessageTypesOnJoin(RocketBinaryReader reader)
    {
      var count = reader.ReadUInt16();
      var messageLookup = new Dictionary<string, Type>();
      foreach (var type in MessageTypes)
      {
        messageLookup[type.FullName] = type;
      }
      var newMessages = new List<Type>();
      for (var i = 0; i < count; i++)
      {
        var name = reader.ReadString();
        if (!messageLookup.TryGetValue(name, out var type))
          throw new Exception($"Unknown message type from server: {name}");
        newMessages.Add(type);
        messageLookup.Remove(name);
      }
      foreach (var key in messageLookup.Keys)
      {
        // if any were not added, report it as missing
        throw new Exception($"Server missing message type: {key}");
      }
      MessageTypes.Clear();
      TypeToIndex.Clear();
      MessageTypes.AddRange(newMessages);
      for (var i = 0; i < count; i++)
      {
        TypeToIndex[MessageTypes[i]] = (ushort)i;
      }
    }
  }

  static class ModNetworkPatches
  {
    [HarmonyPatch(typeof(RocketBinaryReader), nameof(RocketBinaryReader.ReadMessageType)), HarmonyPrefix]
    static bool RocketBinaryReader_ReadMessageType(RocketBinaryReader __instance, ref Type __result)
    {
      __result = ModNetworkMessages.ReadMessageType(__instance);
      return false;
    }

    [HarmonyPatch(typeof(RocketBinaryWriter), nameof(RocketBinaryWriter.WriteMessageType)), HarmonyPrefix]
    static bool RocketBinaryWriter_WriteMessageType(RocketBinaryWriter __instance, Type value)
    {
      ModNetworkMessages.WriteMessageType(__instance, value);
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

    [HarmonyPatch(typeof(AtmosphericsManager), nameof(AtmosphericsManager.SerialiseOnJoin)), HarmonyPostfix]
    static void AtmosphericsManager_SerializeOnJoin(RocketBinaryWriter writer)
    {
      ModNetworkMessages.WriteMessageTypesOnJoin(writer);
    }

    static RocketBinaryReader ClientJoinReader;
    [HarmonyPatch(typeof(AtmosphericsManager), nameof(AtmosphericsManager.DeserializeOnJoin)), HarmonyPrefix]
    static void AtmosphericsManager_DeserializeOnJoin(RocketBinaryReader reader)
    {
      // this is an async method so we cant just put a postfix on it to run after
      // instead we hook into a ProcessAtmospheresClient which is called after the atmospheres are read
      // save the reader here to use in that patch
      ClientJoinReader = reader;
    }

    [HarmonyPatch(typeof(AtmosphericsManager), nameof(AtmosphericsManager.ProcessAtmospheresClient)), HarmonyPrefix]
    static void AtmosphericsManager_ProcessAtmospheresClient()
    {
      if (ClientJoinReader != null)
      {
        // if we have a reader here, we are joining
        ModNetworkMessages.ReadMessageTypesOnJoin(ClientJoinReader);
        ClientJoinReader = null;
      }
    }
  }
}