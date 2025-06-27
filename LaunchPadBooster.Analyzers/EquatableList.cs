using System;
using System.Collections.Generic;
using System.Linq;

namespace LaunchPadBooster.Analyzers
{
  // copied from https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md
  public class EquatableList<T> : List<T>, IEquatable<EquatableList<T>>
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