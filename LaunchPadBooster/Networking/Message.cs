
using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using Assets.Scripts.Networking;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  internal static readonly TypeRegistry<INetworkMessage> messageRegistry = new();

  internal static void RegisterMessage<T>(Mod mod) where T : INetworkMessage, new() =>
    messageRegistry.RegisterType<T>(mod);

  internal static void SendMessageDirect(
    long connectionID, ConnectionMethod connectionMethod, INetworkMessage message)
  {
    var typeID = messageRegistry.TypeIDFor(message.GetType());
    var connection = GetConnection(connectionID);

    DEBUG?.Invoke($"Sending {message.GetType()} message to {connectionID}");

    using scoped var encoder = new PacketEncoder(
      NextSequence, PacketType.Message, SerializeMessage(new MessageHeader { TypeID = typeID }, message));

    var writer = new SpanIO(stackalloc byte[1024]);
    while (encoder.Next(ref writer))
    {
      connection.SendPacketData(writer.Used());
      writer.Position = 0;
    }
  }

  internal static readonly List<ConnectionState> sendAllList = [];
  internal static void SendMessageAll(INetworkMessage message, long excludeConnectionId)
  {
    var typeID = messageRegistry.TypeIDFor(message.GetType());

    DEBUG?.Invoke($"Sending {message.GetType()} to all -{excludeConnectionId}");

    sendAllList.Clear();
    for (var i = 0; i < NetworkBase.Clients.Count; i++)
    {
      var client = NetworkBase.Clients[i];
      if (client.state is ClientState.Disconnected || client.connectionId == excludeConnectionId)
        continue;
      sendAllList.Add(GetConnection(client.connectionId));
    }

    if (sendAllList.Count == 0)
      return;

    using scoped var encoder = new PacketEncoder(
      NextSequence, PacketType.Message, SerializeMessage(new MessageHeader { TypeID = typeID }, message));
    var writer = new SpanIO(stackalloc byte[1024]);

    while (encoder.Next(ref writer))
    {
      for (var i = 0; i < sendAllList.Count; i++)
      {
        var connection = sendAllList[i];
        connection.SendPacketData(writer.Used());
      }
      writer.Position = 0;
    }
  }

  internal static Span<byte> SerializeMessage(MessageHeader header, INetworkMessage message)
  {
    var writer = InitWriter(header);
    message.Serialize(writer);
    return writer.AsSpan();
  }
}

internal partial class ConnectionState
{
  internal void ReceiveMessage()
  {
    var segment = ReadIncoming(out MessageHeader header);
    var typeID = header.TypeID;

    DEBUG?.Invoke($"Received {segment.Count}b {typeID} message from {ConnectionID} {ConnectionMethod}");
    if (Status != ConnectionStatus.Ready)
    {
      Debug.LogWarning($"unexpected {typeID} message on connection {ConnectionID} when status is {Status}");
      return;
    }

    if (!ModNetworking.messageRegistry.CtorFor(typeID, out var ctor))
    {
      Debug.LogWarning($"Received unknown message type {typeID} for mod {GetModName(typeID.ModHash)}");
      return;
    }

    var modMsg = ctor();
    using var msgStream = new MemoryStream(segment.Array, segment.Offset, segment.Count, false);
    using var msgReader = new RocketBinaryReader(msgStream);

    modMsg.Deserialize(msgReader);
    modMsg.Process(ConnectionID);
  }
}

internal struct MessageHeader : IPacket
{
  public TypeID TypeID;

  public void Read(ref SpanIO reader)
  {
    TypeID = reader.ReadTypeID();
  }

  public void Write(ref SpanIO writer)
  {
    writer.WriteTypeID(TypeID);
  }
}