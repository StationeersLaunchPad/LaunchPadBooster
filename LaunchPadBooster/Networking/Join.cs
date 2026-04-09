
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Networking;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal partial class ModNetworking
{
  /*
    Base game join sequence:
    - server sends VerifyPlayerRequest message with server info
    - client sends back VerifyPlayer message with player info and server password
    - server adds client to join queue

    Booster join sequence:
    - server sends VerifyPlayerRequest message with JoinValidateHeader appended
      - client ensures JoinValidateHeader is present/supported, or disconnects
    - client sends back VerifyPlayer message with JoinValidateHeader appended
      - server ensures JoinValidateHeader is present/supported, or disconnects
    - server sends JoinValidate data on booster channel
      - client runs join validation and disconnects if invalid
    - client sends JoinValidate data on booster channel
      - server runs join validation and disconnects if invalid
    - server adds client to join queue
  */

  internal const int MaxTotalJoinValidateSize = 1 << 24;
  internal const float JoinValidateTimeout = 30;

  internal static readonly RocketBinaryWriter JoinValidateCustomWriter = new(MaxTotalJoinValidateSize);

  internal static readonly JoinValidateHeader LocalJoinValidateHeader = new()
  {
    NetworkVersion = NetworkVersion,
    Flags = default,
  };

  internal static JoinValidateData PrepareJoinValidateData(ConnectionState connection)
  {
    DEBUG?.Invoke($"Preparing join validate data for {connection.ConnectionID} {connection.ConnectionMethod}");

    var data = new JoinValidateData();

    var modCount = Instances.Count;
    data.ModList = new JoinValidateModData[modCount];
    for (var i = 0; i < modCount; i++)
    {
      var net = Instances[i];
      var mod = net.mod;
      data.ModList[i] = new()
      {
        Hash = mod.Hash,
        ModID = mod.ID.Name,
        Version = mod.ID.Version,
        Required = net.Required,
      };
    }

    var customEntries = new List<JoinValidateModCustomData.Entry>();
    var writer = JoinValidateCustomWriter;
    writer.Reset();
    foreach (var net in Instances)
    {
      if (net.JoinValidator is not IJoinValidator validator)
        continue;
      var mod = net.mod;
      var start = writer.Position;
      DEBUG?.Invoke($"Serializing custom join validate for {mod.ID.Name} {mod.ID.Version}");
      try
      {
        validator.SerializeJoinValidate(writer);
      }
      catch (Exception ex)
      {
        var message = $"Error serializing custom join validate for {mod.ID.Name} {mod.ID.Version}";
        connection.CloseWithError(message);
        throw new Exception(message, ex);
      }
      customEntries.Add(new()
      {
        ModHash = mod.Hash,
        Offset = start,
        Length = writer.Position - start,
      });
    }
    data.ModCustom = new()
    {
      Entries = [.. customEntries],
      RawData = writer.AsSpan(),
    };

    DEBUG?.Invoke($"Prepared join validate data. {data.ModList?.Length ?? 0} mods, {data.ModCustom.Entries?.Length} +{data.ModCustom.RawData.Length}b custom");

    return data;
  }

  internal static void WriteJoinValidateHeader(RocketBinaryWriter writer)
  {
    DEBUG?.Invoke($"Writing join validate header");
    LocalJoinValidateHeader.Write(writer);
  }

  internal static void ReadJoinValidateHeader(long connectionId, RocketBinaryReader reader)
  {
    DEBUG?.Invoke($"Reading join validate header from {connectionId}");
    var header = new JoinValidateHeader();
    header.Read(reader);
    GetConnection(connectionId).ReceiveJoinValidateHeader(ref header);
  }

  internal static void ReceiveVerifyPlayer(long hostId, NetworkMessages.VerifyPlayer message) =>
    GetConnection(message.OwnerConnectionId).ReceiveVerifyPlayer(message);

  internal static void WriteJoinPrefix(RocketBinaryWriter writer)
  {
    var swriter = new SectionedWriter(writer);
    var start = writer.Position;
    foreach (var net in Instances)
    {
      if (net.JoinPrefixSerializer is not IJoinPrefixSerializer serializer)
        continue;
      swriter.StartSection(net.Hash);
      serializer.SerializeJoinPrefix(writer);
      swriter.FinishSection();
    }
    swriter.Finish();
    DEBUG?.Invoke($"Wrote {writer.Position - start}b join prefix");
  }

  internal static void WriteJoinSuffix(RocketBinaryWriter writer)
  {
    var swriter = new SectionedWriter(writer);
    var start = writer.Position;
    foreach (var net in Instances)
    {
      if (net.JoinSuffixSerializer is not IJoinSuffixSerializer serializer)
        continue;
      swriter.StartSection(net.Hash);
      serializer.SerializeJoinSuffix(writer);
      swriter.FinishSection();
    }
    swriter.Finish();
    DEBUG?.Invoke($"Wrote {writer.Position - start}b join suffix");
  }

  internal static void ReadJoinPrefix(RocketBinaryReader reader)
  {
    var sreader = new SectionedReader(reader);
    var start = sreader.stream.Position;
    while (sreader.Next(out var modHash, out var section))
    {
      if (!InstancesByHash.TryGetValue(modHash, out var net)
          || net.JoinPrefixSerializer is not IJoinPrefixSerializer serializer)
      {
        Debug.LogWarning($"Missing join prefix serializer for {GetModName(modHash)}");
        continue;
      }
      serializer.DeserializeJoinPrefix(section);
    }
    DEBUG?.Invoke($"Read {sreader.stream.Position - start}b join prefix");
  }

  internal async static UniTask ReadJoinSuffix(RocketBinaryReader reader)
  {
    await AtmosphericsManager.DeserializeOnJoin(reader);

    var sreader = new SectionedReader(reader);
    var start = sreader.stream.Position;
    while (sreader.Next(out var modHash, out var section))
    {
      if (!InstancesByHash.TryGetValue(modHash, out var net)
          || net.JoinSuffixSerializer is not IJoinSuffixSerializer serializer)
      {
        Debug.LogWarning($"Missing join suffix serializer for {GetModName(modHash)}");
        continue;
      }
      serializer.DeserializeJoinSuffix(section);
    }
    DEBUG?.Invoke($"Read {sreader.stream.Position - start}b join suffix");
  }
}

internal partial class ConnectionState
{
  internal void ReceiveJoinValidate()
  {
    DEBUG?.Invoke(
      $"Received {Incoming.DecompressedSize}b join validate data from {ConnectionID} {ConnectionMethod}");
    if (Status != ConnectionStatus.JoinValidateRead)
    {
      Debug.LogWarning($"unexpected join validate data on connection {ConnectionID} when status is {Status}");
      return;
    }
    try
    {
      var reader = new SpanIO(IncomingBuffer.AsSpan(0, Incoming.DecompressedSize));

      var join = new JoinValidateData();
      join.Read(ref reader);

      if (!DoJoinValidateModList(join.ModList))
        return;

      if (!DoJoinValidateModCustom(join.ModCustom))
        return;

      Status = ConnectionStatus.Ready;

      if (NetworkManager.IsClient)
      {
        // on client validation success, send our validation data back
        DEBUG?.Invoke($"Client join validate complete. Sending join validate data to server.");
        SendJoinValidateData();
      }
      else
      {
        // on server validation success, complete the join
        DEBUG?.Invoke($"Server join validate for {VerifyPlayer?.Name} {ConnectionID} {ConnectionMethod} complete. Completing join");
        NetworkServer.VerifyConnection(ModNetworking.GetHostId(), VerifyPlayer);
      }
    }
    finally
    {
      if (Status != ConnectionStatus.Ready)
        CloseWithError($"Internal error during join validation");
    }
  }

  private static string GetModName(int modHash) => ModNetworking.GetModName(modHash);

  private bool DoJoinValidateModList(JoinValidateModData[] mods)
  {
    var missingLocal = new List<(string name, string version, int hash)>();
    var missingRemote = new List<(string name, string version, int hash)>();

    var seenRemote = new HashSet<int>();
    var pairs = new List<(ModNetworking, JoinValidateModData)>();
    foreach (var remote in mods ?? [])
    {
      seenRemote.Add(remote.Hash);
      if (ModNetworking.InstancesByHash.TryGetValue(remote.Hash, out var local))
      {
        pairs.Add((local, remote));
        continue;
      }
      if (remote.Required)
        missingLocal.Add((remote.ModID, remote.Version, remote.Hash));
    }
    foreach (var local in ModNetworking.Instances)
    {
      if (seenRemote.Contains(local.Hash))
        continue;
      if (local.Required)
        missingRemote.Add((local.ModID, local.Version, local.Hash));
    }

    var versionMismatch = new List<(string name, string vlocal, string vremote)>();
    var versionError = new List<(string name, string vlocal, string vremote)>();

    foreach (var (local, remote) in pairs)
    {
      try
      {
        if (!local.VersionValidator.ValidateVersion(remote.Version))
          versionMismatch.Add((local.ModID, local.Version, remote.Version));
      }
      catch (Exception ex)
      {
        Debug.LogError($"Error validating version for {local.ModID} {local.Version}");
        Debug.LogException(ex);
        versionError.Add((local.ModID, local.Version, remote.Version));
      }
    }

    if (missingLocal.Count + missingRemote.Count + versionMismatch.Count + versionError.Count == 0)
      return true;

    var (localName, remoteName) = NetworkManager.IsServer ? ("server", "client") : ("client", "server");

    var sb = new StringBuilder();

    foreach (var (name, version, hash) in missingLocal)
      sb.AppendLine($"{localName} missing mod {name}@{version} (hash {hash})");
    foreach (var (name, version, hash) in missingRemote)
      sb.AppendLine($"{remoteName} missing mod {name}@{version} (hash {hash})");

    foreach (var (name, vlocal, vremote) in versionMismatch)
      sb.AppendLine($"{localName} {name}@{vlocal} version incompatible with {remoteName} {name}@{vremote}");
    foreach (var (name, vlocal, vremote) in versionError)
      sb.AppendLine($"Error validating version {localName} {name}@{vlocal} against {remoteName} {name}@{vremote}");

    CloseWithError(sb.ToString());
    return false;
  }

  private bool DoJoinValidateModCustom(JoinValidateModCustomData custom)
  {
    var missingLocal = new List<int>();
    var missingRemote = new List<int>();
    var rejections = new List<(int modHash, string error)>();
    var errors = new List<int>();

    var seenRemote = new HashSet<int>();
    foreach (var remote in custom.Entries ?? [])
    {
      seenRemote.Add(remote.ModHash);
      if (!ModNetworking.InstancesByHash.TryGetValue(remote.ModHash, out var local) ||
          local.JoinValidator is not IJoinValidator validator)
      {
        missingLocal.Add(remote.ModHash);
        continue;
      }
      var buffer = ArrayPool<byte>.Shared.Rent(remote.Length);
      custom.RawData[remote.Offset..(remote.Offset + remote.Length)].CopyTo(buffer);
      using var reader = new RocketBinaryReader(new MemoryStream(buffer, 0, remote.Length, false));
      try
      {
        if (!validator.ProcessJoinValidate(reader, out var message))
          rejections.Add((local.Hash, message));
      }
      catch (Exception ex)
      {
        Debug.LogError($"Error running join validator for {local.ModID}");
        Debug.LogException(ex);
        errors.Add(local.Hash);
      }
      finally
      {
        ArrayPool<byte>.Shared.Return(buffer);
      }
    }

    foreach (var local in ModNetworking.Instances)
    {
      if (local.JoinValidator != null && !seenRemote.Contains(local.Hash))
        missingRemote.Add(local.Hash);
    }

    if (missingLocal.Count + missingRemote.Count + rejections.Count + errors.Count == 0)
      return true;

    var (localName, remoteName) = NetworkManager.IsServer ? ("server", "client") : ("client", "server");

    var sb = new StringBuilder();
    foreach (var modHash in missingLocal)
      sb.AppendLine($"{localName} missing join validator for {GetModName(modHash)}");
    foreach (var modHash in missingRemote)
      sb.AppendLine($"{remoteName} missing join validator for {GetModName(modHash)}");
    foreach (var (modHash, error) in rejections)
      sb.AppendLine($"{localName} {GetModName(modHash)} join validation failed: {error}");
    foreach (var modHash in errors)
      sb.AppendLine($"{localName} {GetModName(modHash)} errored during join validation");

    CloseWithError(sb.ToString());
    return false;
  }

  public void ReceiveJoinValidateHeader(ref JoinValidateHeader header)
  {
    DEBUG?.Invoke($"Received join validate header from {ConnectionID} {ConnectionMethod}");
    if (header.NetworkVersion != ModNetworking.NetworkVersion)
    {
      CloseWithError("Invalid booster networking version");
      return;
    }
    JoinFlags = header.Flags;
    if (NetworkManager.IsServer)
      SendJoinValidateData();

    Status = ConnectionStatus.JoinValidateRead;
    JoinStart = Time.realtimeSinceStartup;
  }

  public static readonly byte[] JoinValidateSendBuffer = new byte[ModNetworking.MaxTotalJoinValidateSize];
  public void SendJoinValidateData()
  {
    var data = ModNetworking.PrepareJoinValidateData(this);
    var fullWriter = new SpanIO(JoinValidateSendBuffer);
    data.Write(ref fullWriter);

    DEBUG?.Invoke($"Sending {fullWriter.Position}b join validate data to {ConnectionID} {ConnectionMethod}");

    using scoped var encoder = new PacketEncoder(
      ModNetworking.NextSequence, PacketType.JoinValidate, fullWriter.Used());

    var writer = new SpanIO(stackalloc byte[1024]);
    while (encoder.Next(ref writer))
    {
      SendPacketData(writer.Used());
      writer.Position = 0;
    }
  }

  public void ReceiveVerifyPlayer(NetworkMessages.VerifyPlayer message)
  {
    DEBUG?.Invoke($"Received VerifyPlayer from {ConnectionID} {ConnectionMethod}");
    VerifyPlayer = new()
    {
      OwnerConnectionId = message.OwnerConnectionId,
      ClientId = message.ClientId,
      Name = message.Name,
      Password = message.Password,
      Version = message.Version,
      ClientConnectionMethod = message.ClientConnectionMethod,
    };
  }
}

[Flags]
internal enum JoinValidateFlags : int { }

// Appended to VerifyPlayer/VerifyPlayerRequest
internal struct JoinValidateHeader
{
  public byte NetworkVersion;
  // these are currently empty, but are reserved space so we can add flags in the future if needed
  public JoinValidateFlags Flags;

  public void Read(RocketBinaryReader reader)
  {
    try
    {
      NetworkVersion = reader.ReadByte();
      // don't try to read any more if version doesn't match
      if (NetworkVersion != ModNetworking.NetworkVersion)
        return;
      Flags = (JoinValidateFlags)reader.ReadInt32();
    }
    catch (EndOfStreamException)
    {
      // if not present or malformed, set NetworkVersion to invalid
      NetworkVersion = 0;
    }
  }

  public readonly void Write(RocketBinaryWriter writer)
  {
    writer.WriteByte(NetworkVersion);
    writer.WriteInt32((int)Flags);
  }
}

internal enum JoinValidateSection : byte
{
  ModList = 0,
  ModCustom = 2,
}

internal ref struct JoinValidateData : IPacket
{
  public JoinValidateModData[] ModList;
  public JoinValidateModCustomData ModCustom;

  public void Read(ref SpanIO reader)
  {
    ModList = null;
    ModCustom = default;
    while (reader.Position < reader.Data.Length)
    {
      var section = (JoinValidateSection)reader.ReadByte();
      var length = reader.ReadInt32();
      var data = reader.Take(length);
      var sreader = new SpanIO(data);

      switch (section)
      {
        case JoinValidateSection.ModList: ReadSection(ref sreader, out ModList); break;
        case JoinValidateSection.ModCustom: ModCustom.Read(ref sreader); break;
        default:
          Debug.LogWarning($"Received unknown join validate section {section}");
          break;
      }
    }
  }

  private static void ReadSection<T>(ref SpanIO reader, out T[] entries) where T : IPacket
  {
    var count = reader.ReadInt32();
    entries = new T[count];
    for (var i = 0; i < count; i++)
      entries[i].Read(ref reader);
  }

  public void Write(ref SpanIO writer)
  {
    if (ModList is not (null or { Length: 0 }))
      WriteSection(ref writer, JoinValidateSection.ModList, ModList);
    if (ModCustom.Entries is not (null or { Length: 0 }))
    {
      var start = StartSectionWrite(ref writer, JoinValidateSection.ModCustom);
      ModCustom.Write(ref writer);
      FinishSectionWrite(ref writer, start);
    }
  }

  private static void WriteSection<T>(ref SpanIO writer, JoinValidateSection section, T[] entries)
    where T : IPacket
  {
    var start = StartSectionWrite(ref writer, section);
    var count = entries.Length;
    writer.WriteInt32(count);
    for (var i = 0; i < count; i++)
      entries[i].Write(ref writer);
    FinishSectionWrite(ref writer, start);
  }

  private static int StartSectionWrite(ref SpanIO writer, JoinValidateSection section)
  {
    writer.WriteByte((byte)section);
    writer.WriteInt32(0); // reserved place for length
    return writer.Position;
  }

  private static void FinishSectionWrite(ref SpanIO writer, int start)
  {
    var end = writer.Position;
    writer.Position = start - 4;
    writer.WriteInt32(end - start);
    writer.Position = end;
  }
}

internal struct JoinValidateModData : IPacket
{
  public int Hash;
  public string ModID;
  public string Version;
  public bool Required;

  public void Read(ref SpanIO reader)
  {
    Hash = reader.ReadInt32();
    ModID = reader.ReadString();
    Version = reader.ReadString();
    Required = reader.ReadBool();
  }

  public void Write(ref SpanIO writer)
  {
    writer.WriteInt32(Hash);
    writer.WriteString(ModID);
    writer.WriteString(Version);
    writer.WriteBool(Required);
  }
}

internal ref struct JoinValidateModCustomData : IPacket
{
  public Entry[] Entries;
  public Span<byte> RawData;

  public void Read(ref SpanIO reader)
  {
    var count = reader.ReadInt32();
    if (Entries?.Length != count)
      Entries = new Entry[count];
    var offset = 0;
    for (var i = 0; i < count; i++)
    {
      ref var entry = ref Entries[i];
      entry.ModHash = reader.ReadInt32();
      entry.Length = reader.ReadInt32();
      entry.Offset = offset;
      offset += entry.Length;
    }

    RawData = reader.Rest();
  }

  public void Write(ref SpanIO writer)
  {
    var count = Entries?.Length ?? 0;
    writer.WriteInt32(count);
    for (var i = 0; i < count; i++)
    {
      ref var entry = ref Entries[i];
      writer.WriteInt32(entry.ModHash);
      writer.WriteInt32(entry.Length);
    }
    writer.WriteSpan(RawData);
  }

  public struct Entry
  {
    public int ModHash;
    public int Length;
    public int Offset; // not serialized
  }
}