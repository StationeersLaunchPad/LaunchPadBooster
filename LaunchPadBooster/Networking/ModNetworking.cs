using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using HarmonyLib;
using UI;
using UnityEngine.Networking;

namespace LaunchPadBooster.Networking
{
  public static class ModNetworking
  {
    private static readonly object initLock = new();
    private static bool initialized;

    private const byte NETWORK_VERSION = 1;

    private static readonly object assignLock = new();
    private static List<Type> MessageTypes;
    private static Dictionary<Type, ushort> MessageTypeToIndex;

    internal static void Initialize()
    {
      lock (initLock)
      {
        if (initialized)
          return;

        var harmony = new Harmony("LaunchPadBooster.Networking");
        harmony.CreateClassProcessor(typeof(ModNetworkPatches), true).Patch();

        // run compatibility patch late to ensure all mod patches are in
        Prefab.OnPrefabsLoaded += () => ModNetworkCompatibilityPatch.RunPatch(harmony);

        initialized = true;
      }
    }

    internal static void EnsureAssignment()
    {
      lock (assignLock)
      {
        if (MessageTypes != null)
          return;

        MessageTypes = new();
        MessageTypeToIndex = new();
        foreach (var mod in Mod.AllMods)
        {
          if (!mod.MultiplayerRequired)
            continue;
          foreach (var msg in mod.NetworkMessageTypes)
          {
            var index = (ushort)MessageTypes.Count;
            MessageTypes.Add(msg);
            MessageTypeToIndex[msg] = index;
          }
        }
      }
    }

    internal static void SerializeServerModInfo(RocketBinaryWriter writer)
    {
      EnsureAssignment();

      writer.WriteByte(NETWORK_VERSION);

      var modInfos = new List<ModInfoFromServer>();
      foreach (var mod in Mod.AllMods)
      {
        if (!mod.MultiplayerRequired)
          continue;
        var info = new ModInfoFromServer
        {
          ID = mod.ID,
          Messages = new()
        };
        foreach (var msg in mod.NetworkMessageTypes)
        {
          info.Messages.Add(new ModNetworkMessageMapping
          {
            Index = MessageTypeToIndex[msg],
            TypeName = msg.FullName,
          });
        }
        modInfos.Add(info);
      }

      writer.WriteUInt16((ushort)modInfos.Count);
      foreach (var info in modInfos)
        info.Serialize(writer);
    }

    private static List<ModInfoFromServer> modsFromServer;
    private static byte serverNetVersion;
    internal static void DeserializeServerModInfo(RocketBinaryReader reader)
    {
      modsFromServer = new();
      serverNetVersion = 0;
      try
      {
        // if the network format has changed, don't try to read the data
        serverNetVersion = reader.ReadByte();
        if (serverNetVersion != NETWORK_VERSION)
          return;

        var count = reader.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
          var info = new ModInfoFromServer();
          info.Deserialize(reader);
          modsFromServer.Add(info);
        }
      }
      catch (EndOfStreamException)
      {
        // if the server doesn't have network mods installed, we won't be able to read the info
        // we'll handle this later when the mod list doesn't match
      }
    }

    internal static bool ValidateModInfoClient()
    {
      if (serverNetVersion != 0 && serverNetVersion != NETWORK_VERSION)
      {
        FailClientJoin($"Incompatible LaunchPadBooster version");
        return false;
      }

      var modsByName = new Dictionary<string, Mod>();
      foreach (var mod in Mod.AllMods)
        modsByName[mod.ID.Name] = mod;

      var indexToMessage = new Dictionary<ushort, Type>();

      foreach (var smod in modsFromServer)
      {
        if (!modsByName.TryGetValue(smod.ID.Name, out var mod))
        {
          FailClientJoin($"Missing mod required by server: {smod.ID.Name}@{smod.ID.Version}");
          return false;
        }
        modsByName.Remove(smod.ID.Name);

        if (!mod.VersionValid(smod.ID.Version))
        {
          FailClientJoin($"Incompatible mod version: {smod.ID.Name} @ {mod.ID.Version}(local) <> {smod.ID.Version}(server)");
          return false;
        }

        var msgByName = new Dictionary<string, Type>();
        foreach (var msg in mod.NetworkMessageTypes)
          msgByName[msg.FullName] = msg;

        foreach (var smsg in smod.Messages)
        {
          if (!msgByName.TryGetValue(smsg.TypeName, out var msg))
          {
            FailClientJoin($"Missing network message type: {mod.ID.Name} {smsg.TypeName}");
            return false;
          }
          msgByName.Remove(smsg.TypeName);
          indexToMessage[smsg.Index] = msg;
        }
        foreach (var msg in msgByName.Keys)
        {
          FailClientJoin($"Server missing network message type: {mod.ID.Name} {msg}");
          return false;
        }
      }
      if (modsByName.Count > 0)
      {
        var errorMsg = new StringBuilder();
        errorMsg.AppendLine("Server missing required mods:");
        foreach (var mod in modsByName.Values)
          errorMsg.Append($" {mod.ID.Name}@{mod.ID.Version}");
        FailClientJoin(errorMsg.ToString());
        return false;
      }

      var messageTypes = new Type[indexToMessage.Count];
      var messageToIndex = new Dictionary<Type, ushort>();
      foreach (var entry in indexToMessage)
      {
        var (index, type) = (entry.Key, entry.Value);
        if (index >= messageTypes.Length)
        {
          FailClientJoin("Internal error: invalid message assignment received from server");
          return false;
        }
        messageTypes[index] = type;
        messageToIndex[type] = index;
      }

      MessageTypes = new(messageTypes);
      MessageTypeToIndex = messageToIndex;

      return true;
    }

    private static void FailClientJoin(string message)
    {
      ConsoleWindow.PrintError(message, true);
      NetworkClient.StopConnectionTimer();
      NetworkManager.EndConnection();
      ConfirmationPanel.Instance.SetUpPanel("Incompatible mods", message, "ButtonOk", NetworkClient.Cancel);
    }

    internal static void SerializeClientModInfo(RocketBinaryWriter writer)
    {
      EnsureAssignment();

      writer.WriteByte(NETWORK_VERSION);

      var modIDs = new List<ModID>();
      foreach (var mod in Mod.AllMods)
      {
        if (!mod.MultiplayerRequired)
          continue;
        modIDs.Add(mod.ID);
      }

      writer.WriteUInt16((ushort)modIDs.Count);
      foreach (var id in modIDs)
        writer.WriteModID(id);
    }

    private static List<ModID> modsFromClient;
    private static byte clientNetVersion;
    internal static void DeserializeClientModInfo(RocketBinaryReader reader)
    {
      modsFromClient = new();
      clientNetVersion = 0;
      try
      {
        // don't try to read data if network version doesn't match
        clientNetVersion = reader.ReadByte();
        if (clientNetVersion != NETWORK_VERSION)
          return;

        var count = reader.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
          var id = reader.ReadModID();
          modsFromClient.Add(id);
        }
      }
      catch (EndOfStreamException)
      {
        // if the client doesn't have network mods installed, we won't be able to read the info
        // we'll handle this later when the mod list doesn't match
      }
    }

    internal static bool ValidateModInfoServer(long hostId, NetworkMessages.VerifyPlayer msg)
    {
      if (clientNetVersion != 0 && clientNetVersion != NETWORK_VERSION)
      {
        RejectClientJoin(hostId, msg, $"Incompatible LaunchPadBooster version");
        return false;
      }

      var modsByName = new Dictionary<string, Mod>();
      foreach (var mod in Mod.AllMods)
        modsByName[mod.ID.Name] = mod;

      foreach (var cmod in modsFromClient)
      {
        if (!modsByName.TryGetValue(cmod.Name, out var mod))
        {
          RejectClientJoin(hostId, msg, $"Server missing required mod: {cmod.Name}@{cmod.Version}");
          return false;
        }
        modsByName.Remove(cmod.Name);

        if (!mod.VersionValid(cmod.Version))
        {
          RejectClientJoin(hostId, msg, $"Incompatible mod version: {cmod.Name} @ {mod.ID.Version}(server) <> {cmod.Version}(client)");
          return false;
        }
      }
      if (modsByName.Count > 0)
      {
        var errorMsg = new StringBuilder();
        errorMsg.AppendLine("Client missing required mods:");
        foreach (var mod in modsByName.Values)
          errorMsg.Append($" {mod.ID.Name}@{mod.ID.Version}");
        RejectClientJoin(hostId, msg, errorMsg.ToString());
        return false;
      }

      return true;
    }

    private static void RejectClientJoin(long hostId, NetworkMessages.VerifyPlayer msg, string message)
    {
      var client = new Client(hostId, msg.OwnerConnectionId, msg.ClientId, msg.Name, msg.ClientConnectionMethod);
      ConsoleWindow.PrintError($"Rejecting client {msg.Name}: {message}", true);
      NetworkServer.SendToClient(new NetworkMessages.Handshake
      {
        HandshakeState = HandshakeType.Rejected,
        Message = message,
      }, NetworkChannel.GeneralTraffic, client);
      NetworkManager.CloseP2PConnectionServer(client);
    }

    public static void WriteMessageType(RocketBinaryWriter writer, Type type)
    {
      if (typeof(IModNetworkMessage).IsAssignableFrom(type))
      {
        writer.WriteByte(255);
        writer.WriteUInt16(MessageTypeToIndex[type]);
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

    internal static void WriteModID(this RocketBinaryWriter writer, ModID id)
    {
      writer.WriteString(id.Name);
      writer.WriteString(id.Version);
    }

    internal static ModID ReadModID(this RocketBinaryReader reader)
    {
      var name = reader.ReadString();
      var version = reader.ReadString();
      return new(name, version);
    }
  }

  public struct ModNetworkMessageMapping
  {
    public ushort Index;
    public string TypeName;

    public void Serialize(RocketBinaryWriter writer)
    {
      writer.WriteUInt16(Index);
      writer.WriteString(TypeName);
    }

    public void Deserialize(RocketBinaryReader reader)
    {
      Index = reader.ReadUInt16();
      TypeName = reader.ReadString();
    }
  }

  public struct ModInfoFromServer
  {
    public ModID ID;
    public List<ModNetworkMessageMapping> Messages;

    public void Serialize(RocketBinaryWriter writer)
    {
      writer.WriteModID(ID);
      var mcount = (ushort)Messages.Count;
      writer.WriteUInt16(mcount);
      for (var i = 0; i < mcount; i++)
      {
        Messages[i].Serialize(writer);
      }
    }

    public void Deserialize(RocketBinaryReader reader)
    {
      ID = reader.ReadModID();

      var mcount = reader.ReadUInt16();
      Messages = new(mcount);
      for (var i = 0; i < mcount; i++)
      {
        var msg = new ModNetworkMessageMapping();
        msg.Deserialize(reader);
        Messages.Add(msg);
      }
    }
  }
}