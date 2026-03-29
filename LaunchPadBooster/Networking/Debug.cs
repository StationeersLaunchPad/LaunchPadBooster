
using System.Runtime.CompilerServices;
using Assets.Scripts;
using UnityEngine;

namespace LaunchPadBooster.Networking;

internal delegate void DebugLogDelegate(
  string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0);

internal partial class ModNetworking
{
  internal static DebugLogDelegate DEBUG = null;

  internal static void DebugLogUnity(string message, string file, int line) =>
    Debug.Log($"LPB: {message} @ {file}:{line}");
  internal static void DebugLogConsole(string message, string file, int line) =>
    ConsoleWindow.Print($"LPB: {message} @ {file}:{line}");
}

internal partial class ConnectionState
{
  internal static DebugLogDelegate DEBUG => ModNetworking.DEBUG;
}