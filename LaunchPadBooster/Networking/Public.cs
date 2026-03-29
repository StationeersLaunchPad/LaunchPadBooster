using Assets.Scripts;
using Assets.Scripts.Networking;
using Cysharp.Threading.Tasks;

namespace LaunchPadBooster.Networking;

public interface IModNetworking
{
  public const int MaxMessageSize = 1 << 20;
  public const float MultipartReceiveTimeout = 1;
  public const float RpcCallTimeout = 30;

  // basic validation settings
  public bool Required { get; set; }
  public IVersionValidator VersionValidator { get; set; }

  // mod message registration
  public void RegisterMessage<T>() where T : INetworkMessage, new();
  public void RegisterRPC<T>() where T : INetworkRPC, new();

  // pre-join validation (confirm mod settings compatible)
  public IJoinValidator JoinValidator { get; set; }

  // join package injection
  public IJoinPrefixSerializer JoinPrefixSerializer { get; set; }
  public IJoinSuffixSerializer JoinSuffixSerializer { get; set; }

  // regular state update injection
  public IUpdatePrefixSerializer UpdatePrefixSerializer { get; set; }
  public IUpdateSuffixSerializer UpdateSuffixSerializer { get; set; }

  // temporary support for old booster networking
  public void RegisterLegacyMessage<T>() where T : ModNetworkMessage<T>, new();
}

public interface INetworkMessage
{
  public void Serialize(RocketBinaryWriter writer);
  public void Deserialize(RocketBinaryReader reader);
  public void Process(long clientId);
}

public interface INetworkRPC
{
  // caller
  public void SerializeCall(RocketBinaryWriter writer);
  public void DeserializeResult(RocketBinaryReader reader);

  // callee
  public void DeserializeCall(RocketBinaryReader reader);
  public UniTask ProcessCall(long clientId);
  public void SerializeResult(RocketBinaryWriter writer);
}

public interface IVersionValidator
{
  public bool ValidateVersion(string version);
}

public interface IJoinValidator
{
  public void SerializeJoinValidate(RocketBinaryWriter writer);
  public bool ProcessJoinValidate(RocketBinaryReader reader, out string error);
}

public interface IJoinPrefixSerializer
{
  public void SerializeJoinPrefix(RocketBinaryWriter writer);
  public void DeserializeJoinPrefix(RocketBinaryReader reader);
}

public interface IJoinSuffixSerializer
{
  public void SerializeJoinSuffix(RocketBinaryWriter writer);
  public void DeserializeJoinSuffix(RocketBinaryReader reader);
}

public interface IUpdatePrefixSerializer
{
  public void SerializeUpdatePrefix(RocketBinaryWriter writer);
  public void DeserializeUpdatePrefix(RocketBinaryReader reader);
}

public interface IUpdateSuffixSerializer
{
  public void SerializeUpdateSuffix(RocketBinaryWriter writer);
  public void DeserializeUpdateSuffix(RocketBinaryReader reader);
}

public static class ModNetworkingExtensions
{
  extension(INetworkMessage message)
  {
    public void SendToHost() =>
      message.SendDirect(ModNetworking.GetHostId(), NetworkClient.ConnectionMethod);
    public void SendToClient(Client client) =>
      message.SendDirect(client.connectionId, client.connectionMethod);
    public void SendDirect(long connectionId, ConnectionMethod connectionMethod) =>
      ModNetworking.SendMessageDirect(connectionId, connectionMethod, message);
    public void SendAll(long excludeConnectionId) =>
      ModNetworking.SendMessageAll(message, excludeConnectionId);
  }

  extension(INetworkRPC rpc)
  {
    public UniTask CallHost() => rpc.CallDirect(ModNetworking.GetHostId(), NetworkClient.ConnectionMethod);
    public UniTask CallClient(Client client) => rpc.CallDirect(client.connectionId, client.connectionMethod);
    public UniTask CallDirect(long connectionId, ConnectionMethod connectionMethod) =>
      ModNetworking.CallRpc(connectionId, connectionMethod, rpc);
  }
}