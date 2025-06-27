using System;
using System.Collections.Generic;
using System.IO;
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
    public static bool IsEqual(long a, long b) => a == b;
    public static bool IsEqual(string a, string b) => a == b;
    public static bool IsEqual<T>(T a, T b) where T : IReferencable => a?.ReferenceId == b?.ReferenceId;
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

    public static void Serialize(RocketBinaryWriter writer, long value) => writer.WriteInt64(value);
    public static void Deserialize(RocketBinaryReader reader, out long value) => value = reader.ReadInt64();

    public static void Serialize(RocketBinaryWriter writer, string value) => writer.WriteString(value);
    public static void Deserialize(RocketBinaryReader reader, out string value) => value = reader.ReadString();

    public static void Serialize<T>(RocketBinaryWriter writer, T value) where T : IReferencable => writer.WriteInt64(value?.ReferenceId ?? 0);
    public static void Deserialize<T>(RocketBinaryReader reader, out T value) where T : class, IReferencable => value = Referencable.Find<T>(reader.ReadInt64());
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
          var connectedReferences = false;

          string stypeString = null;
          if (stype.Value is INamedTypeSymbol namedType)
          {
            stypeString = FullyQualifiedName(namedType);
          }

          foreach (var prop in props)
          {
            if (prop.Networked)
              flagBytes = 1 + prop.NetworkIndex / 8;
            if ((prop.Saved || prop.Networked) && prop.Referencable)
              connectedReferences = true;
          }
          return new ConnectedClass
          {
            Namespace = NamespaceName(clazz.ContainingNamespace),
            ClassName = clazz.Name,
            SaveDataType = stypeString,
            Props = props,
            FlagBytes = flagBytes,
            ConnectedReferences = connectedReferences
          };
        }
      );

      context.RegisterSourceOutput(pipeline, static (context, model) =>
      {
        try
        {
          context.AddSource($"{model.Namespace}.{model.ClassName}.g.cs", model.GenerateCode().ToCodeString());
        }
        catch (Exception ex)
        {
          using (var writer = new StreamWriter("error.log")) writer.Write($"{ex.Message}\n{ex.StackTrace}");
          throw;
        }
      });
    }

    private static EquatableList<ConnectedProperty> GetProps(TypedConstant itype)
    {
      if (itype.Value is not INamedTypeSymbol { TypeKind: TypeKind.Interface } ifaceSymbol)
        throw new Exception($"{itype} is not an interface");
      var res = new EquatableList<ConnectedProperty>();
      var networkCount = 0;
      foreach (var prop in ifaceSymbol.GetMembers().OfType<IPropertySymbol>())
      {
        var saved = false;
        var networked = false;
        var comments = new List<string>();
        var isReferencable = prop.Type.AllInterfaces.Any(iface => iface.Name == "IReferencable");
        foreach (var attr in prop.GetAttributes())
        {
          var fqn = FullyQualifiedName(attr.AttributeClass);
          if (fqn == "LaunchPadBooster.Generated.SavedAttribute")
            saved = true;
          else if (fqn == "LaunchPadBooster.Generated.NetworkedAttribute")
            networked = true;
        }
        res.Add(new ConnectedProperty
        {
          DebugComments = comments,
          TypeName = prop.Type.ToDisplayString(),
          PropName = prop.Name,
          Saved = saved,
          Referencable = isReferencable,
          Networked = networked,
          NetworkIndex = networked ? networkCount : -1,
        });
        if (networked)
          networkCount++;
      }
      return res;
    }
  }
}