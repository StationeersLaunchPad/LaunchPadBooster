
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal readonly struct TypeID(int ModHash, int TypeHash) : IEquatable<TypeID>, IComparable<TypeID>
{
  public readonly int ModHash = ModHash;
  public readonly int TypeHash = TypeHash;

  public int CompareTo(TypeID other) => (ModHash, TypeHash).CompareTo((other.ModHash, other.TypeHash));
  public bool Equals(TypeID other) => (ModHash, TypeHash) == (other.ModHash, other.TypeHash);
  public override bool Equals(object obj) => obj is TypeID other && Equals(other);
  public override int GetHashCode() => (ModHash, TypeHash).GetHashCode();
  public override string ToString() => $"{ModHash}:{TypeHash}";
}

internal class TypeRegistry<T>
{
  internal Dictionary<Type, TypeID> typeIDs = [];
  internal Dictionary<TypeID, Type> types = [];
  internal Dictionary<TypeID, Ctor> ctors = [];

  internal delegate T Ctor();
  internal static T Construct<T2>() where T2 : T, new() => new T2();

  internal void RegisterType<T2>(Mod mod) where T2 : T, new()
  {
    var typeId = new TypeID(mod.Hash, Animator.StringToHash(typeof(T2).FullName));
    typeIDs.Add(typeof(T2), typeId);
    types.Add(typeId, typeof(T2));
    ctors.Add(typeId, Construct<T2>);
  }

  internal TypeID TypeIDFor(Type type)
  {
    if (typeIDs.TryGetValue(type, out var id))
      return id;
    throw new InvalidOperationException($"unregistered {typeof(T).Name} type {type}");
  }

  internal bool CtorFor(TypeID id, out Ctor ctor) => ctors.TryGetValue(id, out ctor);
  internal bool TypeFor(TypeID id, out Type type) => types.TryGetValue(id, out type);
}