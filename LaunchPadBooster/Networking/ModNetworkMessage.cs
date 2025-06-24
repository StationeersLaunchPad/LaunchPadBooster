using Assets.Scripts.Networking;

namespace LaunchPadBooster.Networking
{
  public interface IModNetworkMessage { }

  public abstract class ModNetworkMessage<T> : ProcessedMessage<T>, IModNetworkMessage where T : ModNetworkMessage<T>, new() { }
}