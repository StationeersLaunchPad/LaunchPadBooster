using System;
using System.Runtime.CompilerServices;
using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using HarmonyLib;
using Objects;

namespace LaunchPadBooster.Async
{
  public enum GameTickPosition : int
  {
    Start,
    BeforeAtmosphericTick,
    AfterAtmosphericTick,
    End,
    COUNT
  }

  public static class GameTick
  {
    private static readonly object initlock = new();
    private static bool initialized = false;

    internal static readonly ContinuationQueue[] queues = new ContinuationQueue[(int)GameTickPosition.COUNT];

    public static void Initialize()
    {
      lock (initlock)
      {
        if (initialized)
          return;

        var harmony = new Harmony("LaunchPadBooster.GameTick");
        harmony.CreateClassProcessor(typeof(GameTickPatches), true).Patch();

        for (var i = 0; i < queues.Length; i++)
        {
          queues[i] = new();
        }
        initialized = true;
      }
    }

    public static Awaitable Position(GameTickPosition position)
    {
      if (!initialized)
        throw new Exception("You must call GameTick.Initialize before using GameTick Awaitables");
      return new(position);
    }

    public static Awaitable Start() => Position(GameTickPosition.Start);
    public static Awaitable BeforeAtmosphericTick() => Position(GameTickPosition.BeforeAtmosphericTick);
    public static Awaitable AfterAtmosphericTick() => Position(GameTickPosition.AfterAtmosphericTick);
    public static Awaitable End() => Position(GameTickPosition.End);

    internal static void Execute(GameTickPosition position) => GameTick.queues[(int)position].Execute();

    public readonly struct Awaitable
    {
      public readonly GameTickPosition Position;
      public Awaitable(GameTickPosition position)
      {
        this.Position = position;
      }

      public Awaiter GetAwaiter()
      {
        return new Awaiter(Position);
      }
    }

    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
      public readonly GameTickPosition Position;
      public Awaiter(GameTickPosition position)
      {
        this.Position = position;
      }

      public bool IsCompleted => false;

      public void OnCompleted(Action continuation)
      {
        queues[(int)Position].AddContinuation(continuation);
      }

      public void UnsafeOnCompleted(Action continuation)
      {
        queues[(int)Position].AddContinuation(continuation);
      }

      public void GetResult() { }
    }
  }

  static class GameTickPatches
  {
    [HarmonyPatch(typeof(AtmosphericsController), nameof(AtmosphericsController.HandleMainThreadEvents)), HarmonyPrefix]
    static void AtmosphericsController_HandleMainThreadEvents()
    {
      // first threadpool call on server
      if (GameManager.RunSimulation)
        GameTick.Execute(GameTickPosition.Start);
    }

    [HarmonyPatch(typeof(ThingFire), nameof(ThingFire.UpdateFlames)), HarmonyPrefix]
    static void ThingFire_UpdateFlames()
    {
      // first threadpool call on client
      if (!GameManager.RunSimulation)
        GameTick.Execute(GameTickPosition.Start);
    }

    [HarmonyPatch(typeof(RoomController), nameof(RoomController.ThreadedWork)), HarmonyPostfix]
    static void RoomController_ThreadedWork()
    {
      GameTick.Execute(GameTickPosition.BeforeAtmosphericTick);
      // on clients, no atmospheric processing is done, so just run the after tick here
      if (!GameManager.RunSimulation)
        GameTick.Execute(GameTickPosition.AfterAtmosphericTick);
    }

    [HarmonyPatch(typeof(AtmosphericsManager), nameof(AtmosphericsManager.AtmosphericsNetworksTick)), HarmonyPostfix]
    static void AtmosphericsManager_AtmosphericsNetworkTick()
    {
      if (GameManager.RunSimulation)
        GameTick.Execute(GameTickPosition.AfterAtmosphericTick);
    }

    [HarmonyPatch(typeof(CartridgeManager), nameof(CartridgeManager.CartridgeTick)), HarmonyPostfix]
    static void CartridgeManager_CartridgeTick()
    {
      // last threadpool call on DS
      if (GameManager.IsBatchMode)
        GameTick.Execute(GameTickPosition.End);
    }

    [HarmonyPatch(typeof(LiquidSolver), nameof(LiquidSolver.PrepareLiquidRenderBatches)), HarmonyPostfix]
    static void LiquidSolver_PrepareLiquidRenderBatches()
    {
      // last threadpool call on non-DS
      if (!GameManager.IsBatchMode)
        GameTick.Execute(GameTickPosition.End);
    }
  }
}