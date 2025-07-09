using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LaunchPadBooster.Utils
{
  public static class ReflectionUtils
  {
    // ReflectionUtils.Method(() => default(MyType).MyMethod())
    public static MethodInfo Method<T>(Expression<Func<T>> expr) => Method(expr.Body as MethodCallExpression);
    public static MethodInfo Method(Expression<Action> expr) => Method(expr.Body as MethodCallExpression);
    public static MethodInfo Method(MethodCallExpression call) => VirtualMethodIn(call.Method, call.Object?.Type);

    // ReflectionUtils.AsyncMethod(() => default(MyType).MyAsyncMethod())
    // returns the state machine MoveNext method that contains the actual method body
    public static MethodInfo AsyncMethod<T>(Expression<Func<T>> expr)
    {
      var method = Method(expr);

      var asyncAttr = method.GetCustomAttribute<AsyncStateMachineAttribute>();
      var mapping = asyncAttr.StateMachineType.GetInterfaceMap(typeof(IAsyncStateMachine));

      var moveNext = Method(() => default(IAsyncStateMachine).MoveNext());
      for (var i = 0; i < mapping.InterfaceMethods.Length; i++)
      {
        if (mapping.InterfaceMethods[i] == moveNext)
          return mapping.TargetMethods[i];
      }

      throw new Exception("Could not find async method implementation");
    }

    // ReflectionUtils.PropertyGetter(() => default(MyType).MyProperty)
    public static MethodInfo PropertyGetter<T>(Expression<Func<T>> expr)
    {
      var memberExpr = expr.Body as MemberExpression;
      var prop = memberExpr.Member as PropertyInfo;
      return VirtualMethodIn(prop.GetGetMethod(), memberExpr.Expression.Type);
    }

    // ReflectionUtils.PropertySetter(() => default(MyType).MyProperty)
    public static MethodInfo PropertySetter(Expression<Action> expr)
    {
      var memberExpr = expr.Body as MemberExpression;
      var prop = memberExpr.Member as PropertyInfo;
      return VirtualMethodIn(prop.GetSetMethod(), memberExpr.Expression.Type);
    }

    // ReflectionUtils.Field(() => default(MyType).MyField)
    public static FieldInfo Field<T>(Expression<Func<T>> expr) => (expr.Body as MemberExpression).Member as FieldInfo;

    // ReflectionUtils.Constructor(() => new MyType());
    public static ConstructorInfo Constructor<T>(Expression<Func<T>> expr) => (expr.Body as NewExpression).Constructor;

    // ReflectionUtils.Operator(() => default(MyType) * default(MyType))
    // ReflectionUtils.Operator(() => (MyType2)default(MyType)) // implicit/explicit cast
    // ReflectionUtils.Operator<MyType2>(() => default(MyType)) // implicit only cast
    public static MethodInfo Operator<T>(Expression<Func<T>> expr) => expr.Body switch
    {
      UnaryExpression uexpr => uexpr.Method,
      BinaryExpression bexpr => bexpr.Method,
      _ => throw new Exception($"invalid operator expression type {expr.Body.GetType()}")
    };

    // ReflectionUtils.Method(() => default(MyType).MyMethod()).CreateDelegate<Action<MyType>>()
    public static T CreateDelegate<T>(this MethodInfo method) where T : Delegate =>
      (T)method.CreateDelegate(typeof(T));

    public static MethodInfo VirtualMethodIn(MethodInfo method, Type type)
    {
      if (!method.IsVirtual)
        return method;
      // for virtual methods, we get usually get the reference to the base method, but want the specific implementation
      // search the specific type for a matching implementation
      var allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      return allMethods.First(m => m.GetBaseDefinition() == method.GetBaseDefinition()) ?? method;
    }
  }
}