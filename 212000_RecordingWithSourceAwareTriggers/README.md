# 212000 - Recording with source-aware triggers

This .NET 10 exercise builds on [211000](../211000_RecordingWithXmlTransitions/README.md).
Both exercises select transitions using the current recorder state. In 212000 the
incoming trigger also has an explicit external source, such as `User` or `Device`.
The trigger source is **not** the source state. It is an identifier supplied by
the caller, not an authenticated identity.

## State identity and behavior

**The enum identifies the state; the class implements its behavior.**

- `RecordingMode` defines the supported state identities: `Idle`, `Recording`, and `Paused`.
- State classes such as `IdleState` and `RecordingState` implement lifecycle behavior and state permissions. Each reports its identity through its `Mode` property.
- XML state names are parsed as exact `RecordingMode` names, not discovered from class names. For example, `name="Idle"` resolves to `RecordingMode.Idle`.
- `StateFactory` maps each mode to its state implementation. Renaming a state class does not change its XML identity.

The intended mapping is one implementation per mode. The factory explicitly selects
that implementation; it does not scan for or reject other classes reporting the same
mode. Separately, the XML loader rejects duplicate state declarations. Multiple
instances of the same state class, such as those used by separate recorders, are valid.

## XML configuration

The [trigger catalog](RecordingWithSourceAwareTriggers/recorder-triggers.xml)
declares the allowed `(name, source)` pairs.
enum names; sources are nonempty, case-sensitive strings. The same name can be
declared for more than one source.

```xml
<triggers>
  <trigger name="Pause" source="User" />
  <trigger name="Pause" source="Device" />
</triggers>
```

The [transition table](RecordingWithSourceAwareTriggers/recorder-transitions.xml)
references both fields. Each transition remains nested under its current state:

```xml
<state name="Recording">
  <transition trigger="Pause" source="User" target="Paused" />
  <transition trigger="Pause" source="Device" target="Idle" />
</state>
```

The `Device` transition is an illustrative, configured alternative; it does not
represent a real device protocol. From Recording, `Pause` from `User` enters
Paused, whereas `Pause` from `Device` enters Idle. The normal User-driven
walkthrough, Start guard, state permissions, and lifecycle behavior from 211000
remain in place.

Load the catalog first, pass it to the transition table loader, and supply both
parts of a trigger when dispatching:

```csharp
using var catalogStream = File.OpenRead("recorder-triggers.xml");
var catalog = TriggerCatalog.Load(catalogStream);
using var transitionsStream = File.OpenRead("recorder-transitions.xml");
var table = TransitionTable.Load(transitionsStream, catalog);

var recorder = new Recorder(table, () => true, () => true, () => true, Console.WriteLine);
recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User"));
recorder.TryHandle(new RecordingTrigger(RecordingRequest.Pause, "Device"));
```

The lookup key is `(current state, trigger name, trigger source)`. A valid
catalog entry does not imply a transition in every state. An unmatched trigger
returns `InvalidRequest`, with no fallback by name or by source. A missing or
blank runtime source throws `ArgumentException`.

Both loaders prohibit DTD processing and external resource resolution. The
catalog rejects duplicate identities, invalid names, missing attributes and
unsupported content. The transition loader rejects references absent from the
catalog, duplicate state/trigger identities, undeclared states or targets, and
unsupported guards and XML structure.

## Run

From this exercise directory:

```powershell
dotnet run --project .\RecordingWithSourceAwareTriggers\RecordingWithSourceAwareTriggers.csproj
dotnet test .\RecordingWithSourceAwareTriggers.slnx
```

The tests cover different sources with the same name and current state, missing
or mismatched sources, catalog and transition validation, lifecycle order, and
guards. This is a synchronous, single-threaded teaching example, not a general
state-machine framework.

## Code and tests

- [TriggerCatalog](RecordingWithSourceAwareTriggers/TriggerCatalog.cs) loads and validates the trigger catalog.
- [TransitionTable](RecordingWithSourceAwareTriggers/TransitionTable.cs) loads and looks up transitions.
- [Recorder](RecordingWithSourceAwareTriggers/Recorder.cs) handles guarded dispatch and lifecycle callbacks.
- [Trigger source tests](RecordingWithSourceAwareTriggers.Tests/TriggerSourceTests.cs) exercise the shipped XML, trigger identities, and configuration validation.
