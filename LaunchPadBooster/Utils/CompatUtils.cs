
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace LaunchPadBooster.Utils;

public static class CompatUtils
{
  public static T MakeDelegate<T>(Type type, string name) where T : Delegate =>
    TryMakeDelegate<T>(type, name, out var fn) ? fn :
      throw new InvalidOperationException($"Could not find {type}.{name} matching {typeof(T)}");

  public static bool TryMakeDelegate<T>(Type type, string name, out T compatDelegate) where T : Delegate
  {
    var delInvoke = typeof(T).GetMethod("Invoke");
    var rtype = delInvoke.ReturnType;
    Span<ParameterInfo> inParams = [.. delInvoke.GetParameters()];
    var canBeInstance = inParams.Length > 0 && inParams[0].ParameterType == type;

    MethodInfo bestMatch = null;
    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    if (canBeInstance)
      flags |= BindingFlags.Instance;

    foreach (var method in type.GetMethods(flags))
    {
      if (method.Name != name)
        continue;
      if (method.ReturnType != rtype)
        continue;

      Span<ParameterInfo> mparams = [.. method.GetParameters()];
      if (method.IsStatic && ParamsValid(inParams, mparams))
        bestMatch = PreferredMethod(bestMatch, method);
      else if (canBeInstance && !method.IsStatic && ParamsValid(inParams[1..], mparams))
        bestMatch = PreferredMethod(bestMatch, method);
    }

    if (bestMatch is null)
    {
      compatDelegate = null;
      return false;
    }

    var exInParams = new List<ParameterExpression>();
    foreach (var p in inParams)
      exInParams.Add(Expression.Parameter(p.ParameterType, p.Name));

    var inCount = bestMatch.IsStatic ? exInParams.Count : exInParams.Count - 1;
    var inOffset = bestMatch.IsStatic ? 0 : 1;

    var matchParams = bestMatch.GetParameters();
    var exOutParams = new List<Expression>();
    for (var i = 0; i < inCount; i++)
      exOutParams.Add(exInParams[i + inOffset]);
    for (var i = inCount; i < matchParams.Length; i++)
      exOutParams.Add(Expression.Constant(matchParams[i].DefaultValue, matchParams[i].ParameterType));

    var call = bestMatch.IsStatic
      ? Expression.Call(bestMatch, exOutParams)
      : Expression.Call(exInParams[0], bestMatch, exOutParams);

    compatDelegate = Expression.Lambda<T>(call, bestMatch.Name, exInParams).Compile();
    return true;
  }

  private static bool ParamsValid(Span<ParameterInfo> aparams, Span<ParameterInfo> bparams)
  {
    if (bparams.Length < aparams.Length)
      return false;
    for (var i = 0; i < aparams.Length; i++)
      if (aparams[i].ParameterType != bparams[i].ParameterType)
        return false;
    for (var i = aparams.Length; i < bparams.Length; i++)
      if (!bparams[i].HasDefaultValue)
        return false;
    return true;
  }

  private static MethodInfo PreferredMethod(MethodInfo a, MethodInfo b) => (a, b) switch
  {
    (null, _) => b,
    (_, null) => a,
    ({ IsStatic: true }, { IsStatic: false }) => b,
    ({ IsStatic: false }, { IsStatic: true }) => a,
    _ when a.GetParameters().Length <= b.GetParameters().Length => a,
    _ => b,
  };
}