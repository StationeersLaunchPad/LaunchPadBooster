using System.Collections.Generic;

namespace LaunchPadBooster.Analyzers
{
  public struct ConnectedProperty
  {
    public List<string> DebugComments;
    public string TypeName;
    public string PropName;
    public bool Saved;
    public bool Networked;
    public bool Referencable;
    public int NetworkIndex;

    public IEnumerable<CodeElement> GenerateProperty()
    {
      foreach (var comment in DebugComments ?? new())
        yield return $"// {comment}";

      if (Referencable)
        yield return $"private long _saved{PropName};";

      yield return $@"
        private {TypeName} _{PropName};
        public {TypeName} {PropName}
        {{
          get => _{PropName};
          set
          {{
            if (Comparators.IsEqual(_{PropName}, value)) return;
            _{PropName} = value;";
      if (Networked)
      {
        var bindex = NetworkIndex / 8;
        var bit = 1 << (NetworkIndex & 7);
        yield return $"if (NetworkManager.IsServer) _CustomUpdateFlags[{bindex}] |= {bit};";
      }
      yield return
        $@"}}
        }}";
    }

    public IEnumerable<CodeElement> GenerateBuildUpdate()
    {
      if (!Networked)
        yield break;

      var bindex = NetworkIndex / 8;
      var bit = 1 << (NetworkIndex & 7);
      yield return $"if ((_CustomUpdateFlags[{bindex}] & {bit}) != 0) Serializers.Serialize(writer, _{PropName});";
    }

    public IEnumerable<CodeElement> GenerateProcessUpdate()
    {
      if (!Networked)
        yield break;

      var bindex = NetworkIndex / 8;
      var bit = 1 << (NetworkIndex & 7);

      yield return $"if ((_CustomUpdateFlags[{bindex}] & {bit}) != 0) Serializers.Deserialize(reader, out _{PropName});";
    }

    public IEnumerable<CodeElement> GenerateSerializeOnJoin()
    {
      if (!Networked)
        yield break;
      yield return $"Serializers.Serialize(writer, _{PropName});";
    }

    public IEnumerable<CodeElement> GenerateDeserializeOnJoin()
    {
      if (!Networked)
        yield break;

      if (Referencable)
        yield return $"Serializers.Deserialize(reader, out _saved{PropName});";
      else
        yield return $"Serializers.Deserialize(reader, out _{PropName});";
    }

    public IEnumerable<CodeElement> GenerateSaveDataProp()
    {
      if (!Saved)
        yield break;
      yield return "[XmlElement]";
      if (Referencable)
        yield return $"public long {PropName};";
      else
        yield return $"public {TypeName} {PropName};";
    }

    public IEnumerable<CodeElement> GenerateSerializeSave()
    {
      if (!Saved)
        yield break;
      if (Referencable)
        yield return $"saveData.{PropName} = _{PropName}?.ReferenceId ?? 0;";
      else
        yield return $"saveData.{PropName} = _{PropName};";
    }

    public IEnumerable<CodeElement> GenerateDeserializeSave()
    {
      if (!Saved)
        yield break;
      if (Referencable)
        yield return $"_saved{PropName} = saveData.{PropName};";
      else
        yield return $"_{PropName} = saveData.{PropName};";
    }

    public IEnumerable<CodeElement> GenerateOnFinishedLoad()
    {
      if (Referencable && (Saved || Networked))
        yield return $"_{PropName} = Referencable.Find<{TypeName}>(_saved{PropName});";
    }
  }
}