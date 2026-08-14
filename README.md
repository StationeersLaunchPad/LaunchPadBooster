# LaunchPadBooster

A utility library for easier development of Stationeers mods.

This library is automatically included in StationeersLaunchPad starting in v0.2.0.

See the [StationeersLaunchPad docs site](https://stationeerslaunchpad.github.io/docs/) for more info on writing mods.

## Usage

Most features are accessed through the `Mod` class. Your mod should create one static instance and use it for registering things with the game.

```cs
using LaunchPadBooster;
using System.Collections.Generic;
using UnityEngine;

public class MyMod : MonoBehaviour
{
  public static readonly Mod MOD = new("MyMod", "1.0.0");

  public void OnLoaded(List<GameObject> prefabs)
  {
    // Set up your mod.
  }
}
```

## Reversible mods

StationeersLaunchPad supports an optional reversible lifecycle for mods. A reversible default entrypoint returns `bool` from its existing supported `OnLoaded(...)` signature and provides a parameterless `void OnUnloaded()` method.

LaunchPadBooster cleanup is intentionally explicit:

- the mod removes registrations and mutations it requested through LaunchPadBooster;
- the mod removes changes it made directly, such as Harmony patches or event subscriptions;
- `MOD.Remove()` releases LaunchPadBooster-owned bookkeeping for that `Mod` instance;
- StationeersLaunchPad cleans resources owned by StationeersLaunchPad.

Example:

```cs
using BepInEx.Configuration;
using HarmonyLib;
using LaunchPadBooster;
using System.Collections.Generic;
using UnityEngine;

public class MyMod : MonoBehaviour
{
  public static readonly Mod MOD = new("MyMod", "1.0.0");

  private Harmony harmony;

  public bool OnLoaded(List<GameObject> prefabs, ConfigFile config)
  {
    harmony = new Harmony("MyMod");
    harmony.PatchAll();

    MOD.AddPrefabs(prefabs);
    MOD.AddSaveDataType<MyCustomSaveData>();

    return true;
  }

  public void OnUnloaded()
  {
    harmony?.UnpatchSelf();

    MOD.RemovePrefabs();
    MOD.RemoveSaveDataTypes();
    MOD.RemoveNetworkRegistrations();

    // Remove LPB-owned bookkeeping last.
    MOD.Remove();
  }
}
```

`MOD.Remove()` should normally be called last, after the mod has removed the registrations it requested.

StationeersLaunchPad does not physically unload mod assemblies. A static field such as `public static readonly Mod MOD` can therefore survive an unload. If the same mod is loaded again during the same game process, LaunchPadBooster re-registers the existing `Mod` instance when its mod-facing APIs are used again and creates a fresh networking instance as needed.

### Prefabs

```cs
public void OnLoaded(List<GameObject> prefabs)
{
  MOD.AddPrefabs(prefabs); // register prefabs so they are included when game prefab data is loaded

  // Add setup handlers to run when the full game data is available.
  MOD.SetupPrefabs() // run on all prefabs
    .SetBlueprintMaterials(); // fill in blueprint materials to match builtin blueprints

  MOD.SetupPrefabs("MyPrefabName") // filter to a specific prefab name
    .SetPaintableColor(ColorType.White); // replace the paintable material and all uses of it with a game color

  MOD.SetupPrefabs<MyPrefabType>() // filter to a prefab type
    .SetExitTool(PrefabNames.Drill) // set the deconstruct tool
    .SetEntry2Tool(PrefabNames.IronSheets, index: 1) // chain multiple handlers for this setup filter
    .RunFunc(myPrefab => { // run a custom setup function during prefab registration
      // do something
    })
    .IgnoreEmpty(); // by default a warning message will print if this setup filter didn't match any prefabs
}
```

Before the game prefab load has run, pending prefab registrations and queued prefab setup actions can be removed with:

```cs
MOD.RemovePrefabs();
```

This clears the mod's pending prefab list and queued setup operations and resets the prefab-related multiplayer-required state.

### SaveData

If your prefabs have their own SaveData type instead of using an existing SaveData type from the game, it needs to be added to the list of types.

```cs
MOD.AddSaveDataType<MyCustomSaveData>();
```

A mod can remove the SaveData types it registered with:

```cs
MOD.RemoveSaveDataTypes();
```

### Network Messages

To add custom network messages, implement the `INetworkMessage` or `INetworkRPC` interface.

```cs
public class MyCustomMessage : INetworkMessage
{
  // must have a default constructor
  public MyCustomMessage() { }

  // write data
  public void Serialize(RocketBinaryWriter writer) { }

  // read data
  public void Deserialize(RocketBinaryReader reader) { }

  // handle message
  public void Process(long clientId) { }
}

public class MyCustomRPC : INetworkRPC
{
  // must have a default constructor
  public MyCustomRPC() { }

  // write argument data on caller
  public void SerializeCall(RocketBinaryWriter writer) { }

  // read argument data on callee
  public void DeserializeCall(RocketBinaryReader reader) { }

  // asynchronously process on callee
  public UniTask ProcessCall(long clientId) { }

  // write result data on callee
  public void DeserializeResult(RocketBinaryReader reader) { }

  // read result data on caller
  public void SerializeResult(RocketBinaryWriter writer) { }
}
```

These message types must be registered with your mod.

```cs
MOD.Networking.RegisterMessage<MyCustomMessage>();
MOD.Networking.RegisterRPC<MyCustomRPC>();
```

The mod's registered message, RPC, and legacy-message types can be removed with:

```cs
MOD.RemoveNetworkRegistrations();
```

Call this before `MOD.Remove()` when unloading a mod that registered network types.

### Multiplayer

If your mod adds prefabs or network messages, it will automatically be marked as required for multiplayer. If you don't have either of these, but still make changes that require both server and client to have the mod installed, you can manually mark it as required.

```cs
MOD.Networking.Required = true;
```

On connection, the client and server check that the required mods are present and compatible. The default check requires the version strings to match exactly. Mods can provide a custom version check by implementing the `IVersionValidator` interface.

```cs
public class MyMod : MonoBehaviour, IVersionValidator
{
  public static readonly Mod MOD = new("MyMod", "1.0.0");

  public void OnLoaded(ModData modData)
  {
    MOD.Networking.VersionValidator = this;
  }

  public bool ValidateVersion(string version) => version.StartsWith("1.0.");
}
```

Mods can also add custom validation on join by implementing the `IJoinValidator` interface. The custom join validation will be run on both client and server before the join process is completed.

```cs
public class MyMod : MonoBehaviour, IJoinValidator
{
  public static readonly Mod MOD = new("MyMod", "1.0.0");
  public static ConfigEntry<int> MyConfigValue;

  public void OnLoaded(ConfigFile config)
  {
    MyConfigValue = config.Bind(new ConfigDefinition("MySection", "MyKey"), 0);
    MOD.Networking.JoinValidator = this;
  }

  public void SerializeJoinValidate(RocketBinaryWriter writer)
  {
    // write data for validator
    writer.WriteInt32(MyConfigValue.Value);
  }

  public bool ProcessJoinValidate(RocketBinaryReader reader, out string error)
  {
    // read data and validate
    var remoteValue = reader.ReadInt32();
    if (remoteValue != MyConfigValue.Value)
    {
      // return false and set the error message to reject the connection
      // a null error will display a default "join validation failed" message
      error = $"Config value mismatch: {remoteValue} != {MyConfigValue.Value}";
      return false;
    }

    // if validation succeeds, set the error to null (ignored) and return true
    error = null;
    return true;
  }
}
```

`Required`, prefab-required state, validators, and other per-mod networking configuration belong to the `ModNetworking` instance. `MOD.Remove()` removes that LPB-owned networking instance. If the mod is used again later in the same process, a fresh networking instance is created when `MOD.Networking` is accessed.

### MultiConstructor kits

A structure can be queued for addition to a `MultiConstructor` kit through prefab setup:

```cs
MOD.SetupPrefabs<MyStructure>()
  .AddToMultiConstructorKit("ItemKitExample");
```

The matching setup operation is also available:

```cs
MOD.SetupPrefabs<MyStructure>()
  .RemoveFromMultiConstructorKit("ItemKitExample");
```

For an already-resolved `Structure`, use the utility methods directly:

```cs
myStructure.AddToMultiConstructorKit("ItemKitExample");
myStructure.RemoveFromMultiConstructorKit("ItemKitExample");
```

`RemoveFromMultiConstructorKit()` removes that structure from the selected kit. It only clears the structure's `ToolExit` reference when it still points to that kit.

### Removing LPB bookkeeping

After removing the registrations and mutations owned by the mod, call:

```cs
MOD.Remove();
```

`MOD.Remove()` releases LaunchPadBooster-owned bookkeeping for the `Mod`, including its networking instance and LPB's global mod registration.

It does not undo Harmony patches, event subscriptions, prefab mutations, SaveData registrations, or network type registrations on behalf of the mod. Those should be explicitly reversed first.

### Utilities

`PrefabNames` has constants for the names of common tools and construction materials for easy use in prefab setup.

`PrefabUtils` has a set of methods for finding and setting up prefabs.

```cs
// get a builtin color material
var material = PrefabUtils.GetColorMaterial(ColorType.Orange);

// Extension helper methods
// Replace all instances of the given material in this prefab with a builtin color material
myThing.ReplaceMaterialWithColor(myPrefab.MyMainMaterial, ColorType.White);

// Set deconstruct tool
myStructure.SetExitTool(PrefabNames.Drill); // final deconstruct tool
myStructure.SetExitTool(PrefabNames.Grinder, 1); // deconstruct from buildstate 1 to 0

// Set build tools for second build state (index 1)
myStructure.SetEntryTool(PrefabNames.Screwdriver, 1);
myStructure.SetEntry2Tool(PrefabNames.IronSheets, 1);

// Add/remove a structure from a MultiConstructor kit
myStructure.AddToMultiConstructorKit("ItemKitLogicSwitch");
myStructure.RemoveFromMultiConstructorKit("ItemKitLogicSwitch");

// Lookup existing prefabs to copy some parts of
// These should only be used during setup. While the game is running use Prefab.Find instead
var kit = PrefabUtils.Find<MultiConstructor>("ItemKitLogicSwitch");
kit.Constructables.Add(myPrefab);
```

`ReflectionUtils` has a set of methods for easily getting reflection objects using lambdas.

```cs
// MethodInfo helpers
var method = ReflectionUtils.Method(() => default(MyType).MyMethod()); // instance method
var staticMethod = ReflectionUtils.Method(() => MyType.MyStaticMethod()); // static method
var getter = ReflectionUtils.PropertyGetter(() => default(MyType).Property); // property get function
var setter = ReflectionUtils.PropertySetter(() => default(MyType).Property); // property set function
var addOperator = ReflectionUtils.Operator(() => default(MyType) + default(MyType)); // operator function
var castOperator = ReflectionUtils.Operator(() => (MyType2)default(MyType)); // cast function

// FieldInfo helpers
var field = ReflectionUtils.Field(() => default(MyType).Field); // instance field
var staticField = ReflectionUtils.Field(() => MyType.StaticField); // static field

// The MethodInfo for an async method will just set up an object to hold the state machine state
// AsyncMethod returns the MoveNext function of the state machine that contains the actual async method body
var asyncMethod = ReflectionUtils.AsyncMethod(() => default(MyType).MyAsyncMethod());
```
