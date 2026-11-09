# 213000 - Recording with XML-defined trigger names

This standalone .NET 10 exercise builds on [212000](../212000_RecordingWithSourceAwareTriggers/README.md). Trigger names no longer need matching members in a C# enum: the XML catalog defines the allowed names and their external sources, while the transition table matches by current state, trigger name, and trigger source.

## What the XML owns

The single `RecordingWithXmlDefinedTriggers\recorder-transitions.xml` file declares the initial state, trigger identities, and state-specific transitions:

```xml
<recorder initial="Idle">
  <triggers>
	<trigger name="StartCapture" source="User" />
	<trigger name="PauseCapture" source="User" />
	<trigger name="PauseCapture" source="Device" />
  </triggers>
  <state name="Idle">
	<transition trigger="StartCapture" source="User" target="Recording" guard="StartPermission" />
  </state>
  <state name="Recording">
	<transition trigger="PauseCapture" source="User" target="Paused" />
	<transition trigger="PauseCapture" source="Device" target="Idle" />
  </state>
  <state name="Paused" />
</recorder>
```

`StartCapture` and `PauseCapture` are ordinary XML strings, not `RecordingRequest` enum values. Trigger names and sources are nonempty and case-sensitive. The same name can be declared for multiple sources, with distinct transition outcomes. A declared identity does not imply a transition in every state.

State identities remain the `RecordingMode` enum, and state classes continue to implement lifecycle and permissions. XML does not select or instantiate C# classes. The `StartPermission` guard delegates to the injected start-permission callback and can be attached to a transition from Idle into Recording.

## Runtime use

```csharp
using var configuration = File.OpenRead("recorder-transitions.xml");
var table = TransitionTable.Load(configuration);
var recorder = new Recorder(table, canStart, canLeaveRecording, canEnterPaused, Console.WriteLine);

recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));
recorder.TryHandle(new RecordingTrigger("PauseCapture", "Device"));
```

An unlisted `(name, source)` pair or an identity with no transition from the current state returns `InvalidRequest`, without fallback. A missing or blank runtime name/source throws `ArgumentException`.

## Configuration validation

The loader prohibits DTD processing and external resource resolution. It requires exactly one trigger catalog, rejects duplicate `(name, source)` identities, invalid or empty names/sources, unsupported XML content, unknown enum-based state/target names, undeclared states or targets, duplicate transitions, and unsupported guards. Trigger names themselves are not parsed as enums.

## Run

From this exercise directory:

```powershell
dotnet run --project .\RecordingWithXmlDefinedTriggers\RecordingWithXmlDefinedTriggers.csproj
dotnet test .\RecordingWithXmlDefinedTriggers.Tests\RecordingWithXmlDefinedTriggers.Tests.csproj
dotnet test .\RecordingWithXmlDefinedTriggers.slnx
```

Tests cover custom trigger names not present in any enum, source-aware matching, lifecycle ordering, guards, rejected requests, and malformed or invalid configuration. This remains a synchronous, single-threaded teaching example, not a general-purpose state-machine framework.
