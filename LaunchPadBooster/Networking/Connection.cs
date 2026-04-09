using System;
using System.Buffers;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Networking;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  internal static Dictionary<long, ConnectionState> connections = [];

  internal static ConnectionState GetConnection(long connectionID)
  {
    if (!connections.TryGetValue(connectionID, out var connection))
    {
      Debug.LogWarning($"Received data on unregistered connection {connectionID}");
      connections[connectionID] = connection = new(connectionID, ConnectionMethod.RocketNet);
    }
    return connection;
  }

  internal static void Cleanup()
  {
    // evaluate timeouts on all connections
    foreach (var conn in connections.Values)
      conn.Cleanup();
  }

  internal static void AddConnection(long connectionID, ConnectionMethod connectionMethod)
  {
    connections[connectionID] = new(connectionID, connectionMethod);
  }

  internal static void RemoveConnection(long connectionID)
  {
    if (connections.Remove(connectionID, out var conn))
      conn.Dispose();
  }

  internal static void ClearConnections()
  {
    foreach (var conn in connections.Values)
      conn.Dispose();
    connections.Clear();
  }
}

internal enum ConnectionStatus
{
  Init,
  Closed,
  JoinValidateRead,
  Ready,
}

internal partial class ConnectionState(long ConnectionID, ConnectionMethod ConnectionMethod)
{
  internal readonly long ConnectionID = ConnectionID;
  internal readonly ConnectionMethod ConnectionMethod = ConnectionMethod;
  internal ConnectionStatus Status = ConnectionStatus.Init;

  // initial connection state
  internal JoinValidateFlags JoinFlags;
  internal NetworkMessages.VerifyPlayer VerifyPlayer;
  internal float JoinStart;

  // packet state
  internal StartPacket Incoming;
  internal byte[] IncomingBuffer;
  internal int IncomingOffset;

  // rpc
  internal readonly Dictionary<ulong, RpcState> WaitingRPCs = [];
  internal readonly List<ulong> CleanupList = [];

  internal void SendPacketData(scoped Span<byte> data) =>
    ModNetworking.SendPacket(ConnectionID, ConnectionMethod, data);

  internal void ResetIncoming()
  {
    Incoming = default;
    if (IncomingBuffer != null)
      ArrayPool<byte>.Shared.Return(IncomingBuffer);
    IncomingBuffer = null;
    IncomingOffset = 0;
  }

  internal void CloseWithError(string message)
  {
    if (Status == ConnectionStatus.Closed)
      return;
    Status = ConnectionStatus.Closed;
    if (NetworkManager.IsServer)
    {
      var client = new Client(0, ConnectionID, 0, "", ConnectionMethod);
      ConsoleWindow.PrintError($"Rejecting client {ConnectionID}: {message}", true);
      NetworkServer.SendToClient(new NetworkMessages.Handshake
      {
        HandshakeState = HandshakeType.Rejected,
        Message = message,
      }, NetworkChannel.GeneralTraffic, client);
      NetworkManager.CloseP2PConnectionServer(client);
    }
    else
    {
      ModNetworking.FailClientJoin(message);
    }
  }

  internal void Cleanup()
  {
    if (Status is ConnectionStatus.JoinValidateRead &&
        Time.realtimeSinceStartup - JoinStart > ModNetworking.JoinValidateTimeout)
    {
      CloseWithError("Timeout during join validation");
      return;
    }

    CleanupList.Clear();
    foreach (var (key, state) in WaitingRPCs)
    {
      if (Time.realtimeSinceStartup > state.Timeout)
        CleanupList.Add(key);
    }
    foreach (var key in CleanupList)
    {
      WaitingRPCs.Remove(key, out var state);
      state.Completion.TrySetException(new TimeoutException());
    }
  }

  internal void Dispose()
  {
    Status = ConnectionStatus.Closed;
    if (IncomingBuffer != null)
      ArrayPool<byte>.Shared.Return(IncomingBuffer);
    Incoming = default;
    IncomingBuffer = null;
    IncomingOffset = 0;
  }
}