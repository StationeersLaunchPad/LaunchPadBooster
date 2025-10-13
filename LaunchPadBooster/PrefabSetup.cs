using System;
using System.Collections.Generic;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Motherboards;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace LaunchPadBooster
{
  public interface IPrefabSetup
  {
    internal void Run(IEnumerable<Thing> things);
  }

  public class PrefabSetup<T> : IPrefabSetup
  {
    public readonly string PrefabName;

    private readonly List<Action<T>> _setup = new();
    private readonly string _constructStack;
    private bool _ignoreEmpty = false;

    internal PrefabSetup(string prefabName)
    {
      PrefabName = prefabName;
      _constructStack = Environment.StackTrace;
    }

    void IPrefabSetup.Run(IEnumerable<Thing> things)
    {
      var any = false;
      foreach (var thing in things)
      {
        if (thing is not T typedThing)
          continue;
        if (PrefabName != null && PrefabName != thing.PrefabName)
          continue;
        any = true;
        foreach (var setup in _setup)
        {
          try
          {
            setup(typedThing);
          }
          catch (Exception ex)
          {
            Debug.LogError($"Error setting up prefab {thing.PrefabName}");
            Debug.LogException(ex);
          }
        }
      }
      if (!any && !_ignoreEmpty)
        Debug.LogWarning($"No prefabs matching {typeof(T)} {PrefabName} for setup at\n{_constructStack}");
    }

    public PrefabSetup<T> IgnoreEmpty()
    {
      _ignoreEmpty = true;
      return this;
    }

    public PrefabSetup<T> RunFunc(Action<T> action)
    {
      this._setup.Add(action);
      return this;
    }

    public PrefabSetup<T> SetBlueprintMaterials() => RunFunc(_SetBlueprintMaterials);
    private static void _SetBlueprintMaterials(T prefab)
    {
      var thing = prefab as Thing;
      if (thing.Blueprint == null)
        return;
      var material = PrefabUtils.GetBlueprintMaterial();
      foreach (var renderer in thing.Blueprint.GetComponentsInChildren<MeshRenderer>(true))
      {
        var mats = renderer.sharedMaterials;
        for (var i = 0; i < mats.Length; i++)
          mats[i] = material;
        renderer.sharedMaterials = mats;
      }
    }

    public PrefabSetup<T> SetPaintableColor(ColorType color, bool emissive = false) => RunFunc(prefab => _SetPaintableColor(prefab, color, emissive));
    private static void _SetPaintableColor(T prefab, ColorType color, bool emissive)
    {
      var thing = prefab as Thing;
      if (thing.PaintableMaterial == null)
      {
        Debug.LogWarning($"Prefab {thing.PrefabName} does not have PaintableMaterial set");
        return;
      }
      PrefabUtils.ReplaceMaterialWithColor(thing, thing.PaintableMaterial, color, emissive);
    }

    public PrefabSetup<T> SetEntryTool(string toolName, int index = 0) => RunFunc(prefab => _SetEntryTool(prefab, toolName, index));
    private static void _SetEntryTool(T prefab, string toolName, int index)
    {
      if (!PrefabUtils.CanSetBuildTool(prefab as Thing, toolName, index, out var structure, out var tool))
        return;
      structure.BuildStates[index].Tool.ToolEntry = tool;
    }

    public PrefabSetup<T> SetEntry2Tool(string toolName, int index = 0) => RunFunc(prefab => _SetEntry2Tool(prefab, toolName, index));
    private static void _SetEntry2Tool(T prefab, string toolName, int index)
    {
      if (!PrefabUtils.CanSetBuildTool(prefab as Thing, toolName, index, out var structure, out var tool))
        return;
      structure.BuildStates[index].Tool.ToolEntry2 = tool;
    }

    public PrefabSetup<T> SetExitTool(string toolName, int index = 0) => RunFunc(prefab => _SetExitTool(prefab, toolName, index));
    private static void _SetExitTool(T prefab, string toolName, int index)
    {
      if (!PrefabUtils.CanSetBuildTool(prefab as Thing, toolName, index, out var structure, out var tool))
        return;
      structure.BuildStates[index].Tool.ToolExit = tool;
    }
    
    public PrefabSetup<T> AddToMultiConstructorKit(string kitName, int order = -1) => RunFunc(prefab => _AddToMultiConstructorKit(prefab, kitName, order));
    private static void _AddToMultiConstructorKit(T prefab, string kitName, int order = -1)
    {
      PrefabUtils.AddToMultiConstructorKit(prefab as Structure, kitName, order);
    }
    
  }
}