
using System;
using System.IO;
using Assets.Scripts.Networking;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  internal static readonly TypeRegistry<INetworkRPC> rpcRegistry = new();

  internal static void RegisterRPC<T>(Mod mod) where T : INetworkRPC, new() =>
    rpcRegistry.RegisterType<T>(mod);

  internal readonly static RocketBinaryWriter rpcWriter = new(IModNetworking.MaxMessageSize);

  internal static UniTask CallRpc(
    long connectionID, ConnectionMethod connectionMethod, INetworkRPC rpc)
  {
    var connection = GetConnection(connectionID);
    var sequence = SendRpcCall(connection, rpc);

    return connection.QueueRpc(sequence, rpc);
  }

  internal static ulong SendRpcCall(ConnectionState connection, INetworkRPC rpc)
  {
    var typeID = rpcRegistry.TypeIDFor(rpc.GetType());
    var sequence = NextSequence;

    DEBUG?.Invoke($"Sending {rpc.GetType()} RPC to {connection.ConnectionID}");

    var rpcWriter = InitWriter(new RpcCallHeader { TypeID = typeID });
    rpc.SerializeCall(rpcWriter);

    using scoped var encoder = new PacketEncoder(sequence, PacketType.RpcCall, rpcWriter.AsSpan());
    var writer = new SpanIO(stackalloc byte[1024]);

    while (encoder.Next(ref writer))
    {
      connection.SendPacketData(writer.Used());
      writer.Position = 0;
    }

    return sequence;
  }
}

internal partial class ConnectionState
{
  internal UniTask QueueRpc(ulong sequence, INetworkRPC rpc)
  {
    var state = WaitingRPCs[sequence] = new()
    {
      Rpc = rpc,
      Completion = new(),
      Timeout = Time.realtimeSinceStartup + IModNetworking.RpcCallTimeout
    };
    return state.Completion.Task;
  }

  internal void ReceiveRpcCall()
  {
    var sequence = Incoming.Sequence;
    var segment = ReadIncoming(out RpcCallHeader header);
    var typeID = header.TypeID;

    if (!ModNetworking.rpcRegistry.CtorFor(typeID, out var ctor))
    {
      Debug.LogWarning($"Received unknown rpc type {typeID} for mod {GetModName(typeID.ModHash)}");
      SendRpcError(sequence, "Unknown rpc type");
      return;
    }
    INetworkRPC rpc;
    try
    {

      rpc = ctor();
      using var stream = new MemoryStream(segment.Array, segment.Offset, segment.Count);
      using var reader = new RocketBinaryReader(stream);

      rpc.DeserializeCall(reader);
    }
    catch (Exception ex)
    {
      Debug.LogException(ex);
      SendRpcError(sequence, ex.Message);
      return;
    }

    RunRpcCall(sequence, rpc).Forget();
  }

  internal void ReceiveRpcResult()
  {
    var segment = ReadIncoming(out RpcResultHeader header);
    if (!WaitingRPCs.Remove(header.CallSequence, out var state))
    {
      Debug.LogWarning($"Received unexpected rpc result {header.CallSequence} from {ConnectionID}");
      return;
    }

    using var stream = new MemoryStream(segment.Array, segment.Offset, segment.Count);
    using var reader = new RocketBinaryReader(stream);
    try
    {
      if (header.IsError)
      {
        state.Completion.TrySetException(new Exception(reader.ReadString()));
        return;
      }

      state.Rpc.DeserializeResult(reader);
      state.Completion.TrySetResult();
    }
    catch (Exception ex)
    {
      state.Completion.TrySetException(ex);
    }
  }

  internal async UniTask RunRpcCall(ulong sequence, INetworkRPC rpc)
  {
    try
    {
      await rpc.ProcessCall(ConnectionID);

      SendRpcResult(sequence, rpc);
    }
    catch (Exception ex)
    {
      Debug.LogException(ex);
      SendRpcError(sequence, ex.Message);
    }
  }

  private void SendRpcResult(ulong callSequence, INetworkRPC rpc)
  {
    var writer = ModNetworking.InitWriter(new RpcResultHeader { CallSequence = callSequence, IsError = false });
    rpc.SerializeResult(writer);
    SendRpcResultData(writer.AsSpan());
  }

  private void SendRpcError(ulong callSequence, string message)
  {
    var writer = ModNetworking.InitWriter(new RpcResultHeader { CallSequence = callSequence, IsError = true });
    writer.WriteString(message);
    SendRpcResultData(writer.AsSpan());
  }

  private void SendRpcResultData(Span<byte> data)
  {
    using scoped var encoder = new PacketEncoder(ModNetworking.NextSequence, PacketType.RpcResult, data);
    var writer = new SpanIO(stackalloc byte[1024]);
    while (encoder.Next(ref writer))
    {
      SendPacketData(writer.Used());
      writer.Position = 0;
    }
  }
}

internal class RpcState
{
  public INetworkRPC Rpc;
  public UniTaskCompletionSource Completion;
  public float Timeout;
}

internal struct RpcCallHeader : IPacket
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

internal struct RpcResultHeader : IPacket
{
  public ulong CallSequence;
  public bool IsError;

  public void Read(ref SpanIO reader)
  {
    CallSequence = reader.ReadUInt64();
    IsError = reader.ReadBool();
  }

  public void Write(ref SpanIO writer)
  {
    writer.WriteUInt64(CallSequence);
    writer.WriteBool(IsError);
  }
}
