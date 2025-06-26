using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LaunchPadBooster.Analyzers
{
  [Generator]
  public class ConnectDataGenerator : IIncrementalGenerator
  {
    private static string FullyQualifiedName(ISymbol symbol) => $"{NamespacePrefix(symbol.ContainingNamespace)}{symbol.MetadataName}";
    private static string NamespacePrefix(INamespaceSymbol ns) => ns.IsGlobalNamespace ? "" : ns.ToDisplayString() + ".";
    private static string NamespaceName(INamespaceSymbol ns) => ns.IsGlobalNamespace ? "" : ns.ToDisplayString();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
      context.RegisterPostInitializationOutput(context =>
      {
        context.AddSource("Attributes.g.cs", @"
using System;
namespace LaunchPadBooster.Generated
{
  [AttributeUsage(AttributeTargets.Class)]
  internal class ConnectDataAttribute : Attribute
  {
    public readonly Type InterfaceType;
    public readonly Type SaveDataType;

    public ConnectDataAttribute(Type interfaceType, Type saveDataType = null)
    {
      this.InterfaceType = interfaceType;
      this.SaveDataType = saveDataType;
    }
  }

  [AttributeUsage(AttributeTargets.Property)]
  internal class SavedAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Property)]
  internal class NetworkedAttribute : Attribute { }
}
");
        context.AddSource("Comparators.g.cs", @"
namespace LaunchPadBooster.Generated
{
  internal static partial class Comparators
  {
    public static bool IsEqual(int a, int b) => a == b;
    public static bool IsEqual(string a, string b) => a == b;
  }
}");
        context.AddSource("Serializers.g.cs", @"
using Assets.Scripts.Networking;

namespace LaunchPadBooster.Generated
{
  internal static partial class Serializers
  {
    public static void Serialize(RocketBinaryWriter writer, int value) => writer.WriteInt32(value);
    public static void Deserialize(RocketBinaryReader reader, out int value) => value = reader.ReadInt32();

    public static void Serialize(RocketBinaryWriter writer, string value) => writer.WriteString(value);
    public static void Deserialize(RocketBinaryReader reader, out string value) => value = reader.ReadString();
  }
}");
      });

      var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
        fullyQualifiedMetadataName: "LaunchPadBooster.Generated.ConnectDataAttribute",
        predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
        transform: static (context, _) =>
        {
          var attr = context.Attributes[0];
          var itype = attr.ConstructorArguments[0];
          var stype = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1] : default;
          var clazz = context.TargetSymbol;
          var props = GetProps(itype);
          var flagBytes = 0;

          string stypeString = null;
          if (stype.Value is INamedTypeSymbol namedType)
          {
            stypeString = FullyQualifiedName(namedType);
          }

          foreach (var prop in props)
          {
            if (prop.Networked)
              flagBytes = 1 + prop.NetworkIndex / 8;
          }
          return new ConnectedClass(
            Namespace: NamespaceName(clazz.ContainingNamespace),
            ClassName: clazz.Name,
            SaveDataType: stypeString,
            Props: props,
            FlagBytes: flagBytes
          );
        }
      );

      context.RegisterSourceOutput(pipeline, static (context, model) =>
      {
        var source = new StringBuilder();
        source.Append($@"
using Comparators = LaunchPadBooster.Generated.Comparators;
using Serializers = LaunchPadBooster.Generated.Serializers;
using ThingSaveData = Assets.Scripts.Objects.ThingSaveData;
using NetworkManager = Assets.Scripts.Networking.NetworkManager;
using RocketBinaryReader = Assets.Scripts.Networking.RocketBinaryReader;
using RocketBinaryWriter = Assets.Scripts.Networking.RocketBinaryWriter;
using XmlElementAttribute = System.Xml.Serialization.XmlElementAttribute;
");
        if (model.Namespace != "")
          source.Append($@"
namespace {model.Namespace}
{{");
        source.Append($@"
  partial class {model.ClassName}
  {{
");

        for (var i = 0; i < model.Props.Count; i++)
        {
          var prop = model.Props[i];
          if (prop.Saved && model.SaveDataType == null)
            source.AppendLine($"#error {prop.PropName} is Saved but no SaveData type was provided");
          source.Append(prop.GenerateProperty());
        }

        if (model.FlagBytes > 0)
        {
          var isNetworkUpdate = new StringBuilder("base.IsNetworkUpdate()");
          for (var i = 0; i < model.FlagBytes; i++)
          {
            isNetworkUpdate.Append($" || _CustomUpdateFlags[{i}] > 0");
          }
          source.Append($@"
    private readonly byte[] _CustomUpdateFlags = new byte[{model.FlagBytes}];

    public override bool IsNetworkUpdate() => {isNetworkUpdate};

    public override void BuildUpdate(RocketBinaryWriter writer, ushort networkUpdateType)
    {{
      base.BuildUpdate(writer, networkUpdateType);");
          for (var i = 0; i < model.FlagBytes; i++)
            source.Append($@"
      writer.WriteByte(_CustomUpdateFlags[{i}]);");
          foreach (var prop in model.Props)
            source.Append(prop.GenerateBuildUpdate());
          for (var i = 0; i < model.FlagBytes; i++)
            source.Append($@"
      _CustomUpdateFlags[{i}] = 0;");
          source.Append($@"
    }}

    public override void ProcessUpdate(RocketBinaryReader reader, ushort networkUpdateType)
    {{
      base.ProcessUpdate(reader, networkUpdateType);");
          for (var i = 0; i < model.FlagBytes; i++)
            source.Append($@"
      _CustomUpdateFlags[{i}] = reader.ReadByte();");
          foreach (var prop in model.Props)
            source.Append(prop.GenerateProcessUpdate());
          source.Append($@"
    }}

    public override void SerializeOnJoin(RocketBinaryWriter writer)
    {{
      base.SerializeOnJoin(writer);");
          foreach (var prop in model.Props)
            source.Append(prop.GenerateSerializeOnJoin());
          source.Append($@"
    }}

    public override void DeserializeOnJoin(RocketBinaryReader reader)
    {{
      base.DeserializeOnJoin(reader);");
          foreach (var prop in model.Props)
            source.Append(prop.GenerateDeserializeOnJoin());
          source.Append($@"
    }}");
        }

        if (model.SaveDataType != null)
        {
          source.Append($@"

    public class {model.ClassName}_SaveData : {model.SaveDataType}
    {{");
          foreach (var prop in model.Props)
            source.Append(prop.GenerateSaveDataProp());
          source.Append($@"
    }}

    public override ThingSaveData SerializeSave()
    {{
      ThingSaveData saveData = new {model.ClassName}_SaveData();
      this.InitialiseSaveData(ref saveData);
      return saveData;
    }}

    protected override void InitialiseSaveData(ref ThingSaveData baseData)
    {{
      base.InitialiseSaveData(ref baseData);
      if (baseData is not {model.ClassName}_SaveData saveData)
        return;");
          foreach (var prop in model.Props)
            source.Append(prop.GenerateSerializeSave());
          source.Append($@"
    }}

    public override void DeserializeSave(ThingSaveData baseData)
    {{
      base.DeserializeSave(baseData);
      if (baseData is not {model.ClassName}_SaveData saveData)
        return;");
          foreach (var prop in model.Props)
            source.Append(prop.GenerateDeserializeSave());
          source.Append($@"
    }}");
        }

        source.Append($@"
  }}");
        if (model.Namespace != "")
          source.Append($@"
}}");
        context.AddSource($"{model.Namespace}.{model.ClassName}.g.cs", source.ToString());
      });
    }

    private static EquatableList<InterfaceProp> GetProps(TypedConstant itype)
    {
      if (itype.Value is not INamedTypeSymbol { TypeKind: TypeKind.Interface } ifaceSymbol)
        throw new Exception($"{itype} is not an interface");
      var res = new EquatableList<InterfaceProp>();
      var networkCount = 0;
      foreach (var prop in ifaceSymbol.GetMembers().OfType<IPropertySymbol>())
      {
        var saved = false;
        var networked = false;
        var comments = new List<string>();
        foreach (var attr in prop.GetAttributes())
        {
          var fqn = FullyQualifiedName(attr.AttributeClass);
          comments.Add(fqn);
          if (fqn == "LaunchPadBooster.Generated.SavedAttribute")
            saved = true;
          else if (fqn == "LaunchPadBooster.Generated.NetworkedAttribute")
            networked = true;
        }
        res.Add(new InterfaceProp
        {
          DebugComments = comments,
          TypeName = prop.Type.ToDisplayString(),
          PropName = prop.Name,
          Saved = saved,
          Networked = networked,
          NetworkIndex = networked ? networkCount : -1,
        });
        if (networked)
          networkCount++;
      }
      return res;
    }

    private record ConnectedClass(string Namespace, string ClassName, string SaveDataType, EquatableList<InterfaceProp> Props, int FlagBytes);

    private struct InterfaceProp
    {
      public List<string> DebugComments;
      public string TypeName;
      public string PropName;
      public bool Saved;
      public bool Networked;
      public int NetworkIndex;

      public string GenerateProperty()
      {
        var source = new StringBuilder();
        foreach (var comment in DebugComments ?? new())
          source.Append($"\n//{comment}");
        source.Append($@"
    private {TypeName} _{PropName};
    public {TypeName} {PropName}
    {{
      get => _{PropName};
      set
      {{
        if (Comparators.IsEqual(_{PropName}, value))
          return;
        _{PropName} = value;
");

        if (Networked)
        {
          var bindex = NetworkIndex / 8;
          var bit = 1 << (NetworkIndex & 7);
          source.Append(@$"
        if (NetworkManager.IsServer)
          _CustomUpdateFlags[{bindex}] |= {bit};
");
        }

        source.Append(@$"
      }}
    }}
");
        return source.ToString();
      }

      public string GenerateBuildUpdate()
      {
        if (!Networked)
          return "";

        var bindex = NetworkIndex / 8;
        var bit = 1 << (NetworkIndex & 7);

        return $@"
      if ((_CustomUpdateFlags[{bindex}] & {bit}) != 0)
        Serializers.Serialize(writer, _{PropName});";
      }

      public string GenerateProcessUpdate()
      {
        if (!Networked)
          return "";

        var bindex = NetworkIndex / 8;
        var bit = 1 << (NetworkIndex & 7);

        return $@"
      if ((_CustomUpdateFlags[{bindex}] & {bit}) != 0)
        Serializers.Deserialize(reader, out _{PropName});";
      }

      public string GenerateSerializeOnJoin()
      {
        if (!Networked)
          return "";

        return $@"
      Serializers.Serialize(writer, _{PropName});";
      }

      public string GenerateDeserializeOnJoin()
      {
        if (!Networked)
          return "";

        return $@"
      Serializers.Deserialize(reader, out _{PropName});";
      }

      public string GenerateSaveDataProp()
      {
        if (!Saved)
          return "";
        return $@"
      [XmlElement]
      public {TypeName} {PropName};";
      }

      public string GenerateSerializeSave()
      {
        if (!Saved)
          return "";
        return $@"
      saveData.{PropName} = _{PropName};";
      }

      public string GenerateDeserializeSave()
      {
        if (!Saved)
          return "";
        return $@"
      _{PropName} = saveData.{PropName};";
      }
    }

    // copied from https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md
    private class EquatableList<T> : List<T>, IEquatable<EquatableList<T>>
    {
      public bool Equals(EquatableList<T> other)
      {
        if (other is null || this.Count != other.Count)
          return false;

        for (var i = 0; i < this.Count; i++)
          if (!EqualityComparer<T>.Default.Equals(this[i], other[i]))
            return false;
        return true;
      }

      public override bool Equals(object obj) => Equals(obj as EquatableList<T>);
      public override int GetHashCode() => this.Select(item => item?.GetHashCode() ?? 0).Aggregate((x, y) => x ^ y);
      public static bool operator ==(EquatableList<T> list1, EquatableList<T> list2) =>
        ReferenceEquals(list1, list2) || list1 is not null && list2 is not null && list1.Equals(list2);
      public static bool operator !=(EquatableList<T> list1, EquatableList<T> list2) => !(list1 == list2);
    }
  }
}