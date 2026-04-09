
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Assets.Scripts.Networking;
using LZ4;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  internal const byte NetworkVersion = 2;
  internal const NetworkChannel BoosterNetworkChannel = (NetworkChannel)169;
  internal const int MaxDataPerPacket = 1024 - 64; // room for header and a bit extra

  internal static ulong NextSequence => ++field;

  internal static string GetModName(int modHash)
  {
    if (Mod.ModsByHash.TryGetValue(modHash, out var mod))
      return mod.ID.Name;
    return $"Unknown Mod {modHash}";
  }

  internal static Func<long> GetHostId => field ??=
    Expression.Lambda<Func<long>>(Expression.Field(null,
      typeof(NetworkManager).GetField(
        "_hostId", BindingFlags.Static | BindingFlags.NonPublic))
    ).Compile();

  internal static void ReceivePacket(long connectionID, Span<byte> data)
  {
    DEBUG?.Invoke($"received {data.Length}b packet from {connectionID}");
    GetConnection(connectionID).ReceivePacket(data);
  }

  internal static void SendPacket(long connectionID, ConnectionMethod connectionMethod, Span<byte> data)
  {
    if (data.Length < 8)
      throw new InvalidOperationException($"{data.Length}");
    DEBUG?.Invoke($"sending {data.Length}b packet #{new SpanIO(data).ReadUInt64():X} to {connectionID} {connectionMethod}");
    NetworkManager.SendNetworkDataDirect(connectionID, connectionMethod, BoosterNetworkChannel, data);
  }

  internal readonly static RocketBinaryWriter messageWriter = new(IModNetworking.MaxMessageSize);

  internal static RocketBinaryWriter InitWriter<T>(T header) where T : IPacket
  {
    messageWriter.Position = 1024;
    var writer = new SpanIO(messageWriter.AsSpan());
    header.Write(ref writer);
    messageWriter.Reset();
    messageWriter.Position = writer.Position;
    return messageWriter;
  }
}

internal partial class ConnectionState
{
  internal void ReceivePacket(Span<byte> data)
  {
    var reader = new SpanIO(data);
    var sequence = reader.ReadUInt64();
    reader.Position = 0;

    if ((sequence & IPacket.ContinueFlag) == 0)
    {
      ResetIncoming();
      var packet = new StartPacket();
      packet.Read(ref reader);
      var pdata = reader.Rest();

      Incoming = packet;
      IncomingBuffer = ArrayPool<byte>.Shared.Rent(packet.CompressedSize);
      pdata.CopyTo(IncomingBuffer);
      IncomingOffset = pdata.Length;
    }
    else
    {
      var packet = new ContinuePacket();
      packet.Read(ref reader);
      var pdata = reader.Rest();

      sequence = packet.Sequence & ~IPacket.ContinueFlag;
      if (sequence != Incoming.Sequence || packet.Offset != IncomingOffset || IncomingBuffer is null)
      {
        Debug.LogWarning($"Unexpected continue packet {sequence}@{packet.Offset}. Expected {Incoming.Sequence}@{IncomingOffset}");
        return;
      }
      pdata.CopyTo(IncomingBuffer.AsSpan(IncomingOffset));
      IncomingOffset += pdata.Length;
    }

    if (IncomingOffset < Incoming.CompressedSize)
      return;
    try
    {
      CompleteReceive();
    }
    finally
    {
      ResetIncoming();
    }
  }

  internal void CompleteReceive()
  {
    if (Incoming.CompressedSize < Incoming.DecompressedSize)
    {
      var cbuffer = IncomingBuffer;
      IncomingBuffer = ArrayPool<byte>.Shared.Rent(Incoming.DecompressedSize);
      LZ4Codec.Decode(cbuffer, 0, Incoming.CompressedSize, IncomingBuffer, 0, Incoming.DecompressedSize, true);
      ArrayPool<byte>.Shared.Return(cbuffer);
    }

    switch (Incoming.Type)
    {
      case PacketType.JoinValidate: ReceiveJoinValidate(); break;
      case PacketType.Message: ReceiveMessage(); break;
      case PacketType.RpcCall: ReceiveRpcCall(); break;
      case PacketType.RpcResult: ReceiveRpcResult(); break;
      default:
        Debug.LogWarning($"Received unknown booster message {Incoming.Type} from {ConnectionID} {ConnectionMethod}");
        break;
    }
  }

  internal ArraySegment<byte> ReadIncoming<T>(out T header) where T : IPacket, new()
  {
    var reader = new SpanIO(IncomingBuffer.AsSpan(0, Incoming.DecompressedSize));
    header = new();
    header.Read(ref reader);

    return new(IncomingBuffer, reader.Position, reader.Rest().Length);
  }
}

internal enum PacketType : ushort
{
  JoinValidate = 1,
  Message = 2,
  RpcCall = 3,
  RpcResult = 4,
}

internal interface IPacket
{
  public const ulong ContinueFlag = 1u << 63;

  public void Write(ref SpanIO writer);
  public void Read(ref SpanIO reader);
}

internal struct StartPacket : IPacket
{
  public ulong Sequence;
  public PacketType Type;
  public int CompressedSize;
  public int DecompressedSize;

  public void Read(ref SpanIO reader)
  {
    Sequence = reader.ReadUInt64();
    Type = reader.ReadPacketType();
    CompressedSize = reader.ReadInt32();
    DecompressedSize = reader.ReadInt32();
  }

  public void Write(ref SpanIO writer)
  {
    writer.WriteUInt64(Sequence);
    writer.WritePacketType(Type);
    writer.WriteInt32(CompressedSize);
    writer.WriteInt32(DecompressedSize);
  }
}

internal struct ContinuePacket : IPacket
{
  public ulong Sequence;
  public int Offset;

  public void Read(ref SpanIO reader)
  {
    Sequence = reader.ReadUInt64();
    Offset = reader.ReadInt32();
  }

  public void Write(ref SpanIO writer)
  {
    writer.WriteUInt64(Sequence);
    writer.WriteInt32(Offset);
  }
}

internal ref struct SpanIO(Span<byte> Data)
{
  public readonly Span<byte> Data = Data;
  public int Position = 0;

  public Span<byte> Take(int sz)
  {
    var start = Position;
    Position += sz;
    return Data[start..Position];
  }

  public Span<byte> Used() => Data[..Position];
  public Span<byte> Rest() => Take(Data.Length - Position);

  public bool ReadBool() => Take(1)[0] != 0;
  public byte ReadByte() => Take(1)[0];
  public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
  public PacketType ReadPacketType() => (PacketType)ReadUInt16();
  public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
  public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
  public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));
  public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
  public unsafe string ReadString() => Encoding.UTF8.GetString(Take(ReadInt32()));
  public TypeID ReadTypeID() => new(ReadInt32(), ReadInt32());

  public void WriteBool(bool val) => Take(1)[0] = (byte)(val ? 1 : 0);
  public void WriteByte(byte val) => Take(1)[0] = val;
  public void WriteUInt16(ushort val) => BinaryPrimitives.WriteUInt16LittleEndian(Take(2), val);
  public void WritePacketType(PacketType val) => WriteUInt16((ushort)val);
  public void WriteInt32(int val) => BinaryPrimitives.WriteInt32LittleEndian(Take(4), val);
  public void WriteUInt32(uint val) => BinaryPrimitives.WriteUInt32LittleEndian(Take(4), val);
  public void WriteInt64(long val) => BinaryPrimitives.WriteInt64LittleEndian(Take(8), val);
  public void WriteUInt64(ulong val) => BinaryPrimitives.WriteUInt64LittleEndian(Take(8), val);
  public void WriteSpan(scoped ReadOnlySpan<byte> data) => data.CopyTo(Take(data.Length));
  public unsafe void WriteString(string val)
  {
    var length = Encoding.UTF8.GetByteCount(val);
    Span<byte> strData = stackalloc byte[length];
    Encoding.UTF8.GetBytes(val, strData);
    WriteInt32(length);
    WriteSpan(strData);
  }
  public void WriteTypeID(TypeID id)
  {
    WriteInt32(id.ModHash);
    WriteInt32(id.TypeHash);
  }
}

internal ref struct PacketEncoder : IDisposable
{
  internal readonly ulong sequence;
  internal readonly PacketType type;
  internal readonly byte[] buffer;
  internal readonly int compSize;
  internal readonly int decompSize;
  internal int offset = 0;
  internal bool started = false;

  internal PacketEncoder(ulong sequence, PacketType type, Span<byte> data)
  {
    this.sequence = sequence;
    this.type = type;

    decompSize = data.Length;
    buffer = ArrayPool<byte>.Shared.Rent(decompSize);
    data.CopyTo(buffer);
    if (decompSize <= ModNetworking.MaxDataPerPacket)
      compSize = decompSize;
    else
    {
      var cdata = ArrayPool<byte>.Shared.Rent(LZ4Codec.MaximumOutputLength(decompSize));
      compSize = LZ4Codec.Encode(buffer, 0, decompSize, cdata, 0, cdata.Length);
      if (compSize < decompSize)
      {
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = cdata;
      }
      else
      {
        compSize = decompSize;
        ArrayPool<byte>.Shared.Return(cdata);
      }
    }
  }

  internal Span<byte> Chunk()
  {
    var len = Math.Min(compSize - offset, ModNetworking.MaxDataPerPacket);
    var chunk = buffer.AsSpan(offset, len);
    offset += len;
    return chunk;
  }

  internal bool Next(ref SpanIO writer)
  {
    if (offset == compSize && !started)
      return false;

    if (offset == 0)
    {
      new StartPacket
      {
        Sequence = sequence,
        Type = type,
        CompressedSize = compSize,
        DecompressedSize = decompSize,
      }.Write(ref writer);
    }
    else
    {
      new ContinuePacket
      {
        Sequence = sequence | IPacket.ContinueFlag,
        Offset = offset,
      }.Write(ref writer);
    }
    writer.WriteSpan(Chunk());
    return true;
  }

  public void Dispose()
  {
    ArrayPool<byte>.Shared.Return(buffer);
  }
}

internal struct SectionedWriter
{
  internal readonly RocketBinaryWriter writer;
  internal readonly int countPos;
  internal int count = 0;
  internal int sectionPos = 0;

  internal SectionedWriter(RocketBinaryWriter writer)
  {
    this.writer = writer;
    countPos = writer.Position;
    writer.WriteInt32(0);
  }

  internal void StartSection(int modHash)
  {
    count++;
    writer.WriteInt32(modHash);
    writer.WriteInt32(0);
    sectionPos = writer.Position;
  }

  internal void FinishSection()
  {
    writer.Position = sectionPos - 4;
    writer.WriteInt32(writer.Length - sectionPos);
    writer.Position = writer.Length;
  }

  internal void Finish()
  {
    writer.Position = countPos;
    writer.WriteInt32(count);
    writer.Position = writer.Length;
  }
}

internal struct SectionedReader
{
  private static readonly FieldInfo readerStreamField =
    typeof(RocketBinaryReader).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(
      f => typeof(Stream).IsAssignableFrom(f.FieldType) && f.FieldType.IsAssignableFrom(typeof(MemoryStream))
    );
  private static readonly FieldInfo streamExposedField =
    typeof(MemoryStream).GetField("_exposable", BindingFlags.Instance | BindingFlags.NonPublic);

  internal readonly RocketBinaryReader fullReader;
  internal readonly MemoryStream stream;
  internal readonly byte[] buffer;

  internal int remaining;

  internal SectionedReader(RocketBinaryReader fullReader)
  {
    this.fullReader = fullReader;
    stream = (MemoryStream)readerStreamField.GetValue(fullReader);
    streamExposedField.SetValue(stream, true);
    buffer = stream.GetBuffer();

    remaining = fullReader.ReadInt32();
  }

  internal bool Next(out int modHash, out RocketBinaryReader reader)
  {
    if (remaining == 0)
    {
      modHash = 0;
      reader = null;
      return false;
    }
    remaining--;
    modHash = fullReader.ReadInt32();
    var length = fullReader.ReadInt32();
    var index = (int)stream.Position;
    reader = new(new MemoryStream(buffer, index, length, false));
    stream.Seek(length, SeekOrigin.Current);
    return true;
  }
}