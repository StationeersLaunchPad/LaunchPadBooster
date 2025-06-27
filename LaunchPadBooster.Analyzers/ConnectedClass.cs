using System.Collections.Generic;
using System.Text;

namespace LaunchPadBooster.Analyzers
{
  public struct ConnectedClass
  {
    public string Namespace;
    public string ClassName;
    public string SaveDataType;
    public EquatableList<ConnectedProperty> Props;
    public int FlagBytes;
    public bool ConnectedReferences;

    public IEnumerable<CodeElement> GenerateCode()
    {
      yield return $@"
        using Comparators = LaunchPadBooster.Generated.Comparators;
        using Serializers = LaunchPadBooster.Generated.Serializers;
        using ThingSaveData = Assets.Scripts.Objects.ThingSaveData;
        using NetworkManager = Assets.Scripts.Networking.NetworkManager;
        using RocketBinaryReader = Assets.Scripts.Networking.RocketBinaryReader;
        using RocketBinaryWriter = Assets.Scripts.Networking.RocketBinaryWriter;
        using XmlElementAttribute = System.Xml.Serialization.XmlElementAttribute;
      ";

      if (Namespace != "")
      {
        yield return
        $@"namespace {Namespace}
          {{";
        yield return GenerateClass().Element();
        yield return "}";
      }
      else
      {
        yield return GenerateClass().Element();
      }
    }

    private IEnumerable<CodeElement> GenerateClass()
    {
      yield return
      @$"partial class {ClassName}
        {{
      ";
      {
        foreach (var prop in Props)
        {
          yield return prop.GenerateProperty().Element();
        }

        if (FlagBytes > 0)
          yield return GenerateNetworking().Element();

        if (SaveDataType != null)
          yield return GenerateSave().Element();

        if (ConnectedReferences)
          yield return GenerateOnFinishedLoad().Element();
      }
      yield return "}";
    }

    private IEnumerable<CodeElement> GenerateNetworking()
    {
      var updateVal = new StringBuilder("base.IsNetworkUpdate()");
      for (var i = 0; i < FlagBytes; i++)
        updateVal.Append($"|| _CustomUpdateFlags[{i}] > 0");
      yield return $@"
        private readonly byte[] _CustomUpdateFlags = new byte[{FlagBytes}];
        
        public override bool IsNetworkUpdate() => {updateVal};
        
        public override void BuildUpdate(RocketBinaryWriter writer, ushort networkUpdateType)
        {{
          base.BuildUpdate(writer, networkUpdateType);";
      for (var i = 0; i < FlagBytes; i++)
        yield return $"writer.WriteByte(_CustomUpdateFlags[{i}]);";
      foreach (var prop in Props)
        yield return prop.GenerateBuildUpdate().Element();
      for (var i = 0; i < FlagBytes; i++)
        yield return $"_CustomUpdateFlags[{i}] = 0;";
      yield return
        $@"}}
        
        public override void ProcessUpdate(RocketBinaryReader reader, ushort networkUpdateType)
        {{
          base.ProcessUpdate(reader, networkUpdateType);";
      for (var i = 0; i < FlagBytes; i++)
        yield return $"_CustomUpdateFlags[{i}] = reader.ReadByte();";
      foreach (var prop in Props)
        yield return prop.GenerateProcessUpdate().Element();
      yield return
        $@"}}
        
        public override void SerializeOnJoin(RocketBinaryWriter writer)
        {{
          base.SerializeOnJoin(writer);";
      foreach (var prop in Props)
        yield return CodeElement.List(prop.GenerateSerializeOnJoin());
      yield return "}";
    }

    private IEnumerable<CodeElement> GenerateSave()
    {
      yield return $@"
        public class {ClassName}_SaveData : {SaveDataType}
        {{";
      foreach (var prop in Props)
        yield return prop.GenerateSaveDataProp().Element();
      yield return
        $@"}}
        
        public override ThingSaveData SerializeSave()
        {{
          ThingSaveData saveData = new {ClassName}_SaveData();
          this.InitialiseSaveData(ref saveData);
          return saveData;
        }}
        
        protected override void InitialiseSaveData(ref ThingSaveData baseData)
        {{
          base.InitialiseSaveData(ref baseData);
          if (baseData is not {ClassName}_SaveData saveData) return;";
      foreach (var prop in Props)
        yield return prop.GenerateSerializeSave().Element();
      yield return
        $@"}}
        
        public override void DeserializeSave(ThingSaveData baseData)
        {{
          base.DeserializeSave(baseData);
          if (baseData is not {ClassName}_SaveData saveData) return;";
      foreach (var prop in Props)
        yield return prop.GenerateDeserializeSave().Element();
      yield return "}";
    }

    private IEnumerable<CodeElement> GenerateOnFinishedLoad()
    {
      yield return $@"
        public override void OnFinishedLoad()
        {{";
      foreach (var prop in Props)
        yield return prop.GenerateOnFinishedLoad().Element();
      yield return "}";
    }
  }
}