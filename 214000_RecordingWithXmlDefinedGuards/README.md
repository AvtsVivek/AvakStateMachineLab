# 214000 - Recording with XML-defined guard names

This standalone .NET 10 exercise builds on [213000](../213000_RecordingWithXmlDefinedTriggers/README.md). Guard names are defined in XML and resolved through an injected guard registry, while guard logic remains in C#.

## What the XML owns

The single `RecordingWithXmlDefinedGuards\recorder-transitions.xml` file declares initial state, trigger identities, state-specific transitions, and guard attachment:

```xml
<recorder initial="Idle">
  <triggers>
    <trigger name="StartCapture" source="User" />
    <trigger name="PauseCapture" source="User" />
    <trigger name="PauseCapture" source="Device" />
    <trigger name="ResumeCapture" source="User" />
    <trigger name="StopCapture" source="User" />
  </triggers>
  <state name="Idle">
    <transition trigger="StartCapture" source="User" target="Recording" guard="StartPermission" />
  </state>
  <state name="Recording">
    <transition trigger="PauseCapture" source="User" target="Paused" />
    <transition trigger="PauseCapture" source="Device" target="Idle" />
    <transition trigger="StopCapture" source="User" target="Idle" />
  </state>
  <state name="Paused">
    <transition trigger="ResumeCapture" source="User" target="Recording" />
    <transition trigger="StopCapture" source="User" target="Idle" />
  </state>
</recorder>
```

Guard names like `StartPermission` are now arbitrary strings declared in XML. The XML does not define behavior—it only names guards and associates them with transitions. Guard logic resides in the injected `GuardRegistry`.

## Runtime use

```csharp
using var configuration = File.OpenRead("recorder-transitions.xml");
var table = TransitionTable.Load(configuration);

var guardRegistry = new GuardRegistry();
guardRegistry.Register("StartPermission", () => canStartRecording());
guardRegistry.Register("RecordingExit", () => canLeaveRecording());
guardRegistry.Register("PausedEntry", () => canEnterPaused());

var recorder = new Recorder(table, guardRegistry, Console.WriteLine);

recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));
recorder.TryHandle(new RecordingTrigger("PauseCapture", "Device"));
```

The loader enforces that every guard name used in the XML is registered at runtime. A missing or unregistered guard name throws `ArgumentException` when the transition is evaluated. An unlisted `(name, source)` pair still returns `InvalidRequest`.

## Configuration validation

The loader prohibits DTD processing and external resource resolution. It requires exactly one trigger catalog, rejects duplicate `(name, source)` identities, invalid or empty names/sources/guard names, unsupported XML content, unknown enum-based state/target names, undeclared states or targets, duplicate transitions, and references to guards in unsupported state combinations. Trigger and guard names themselves are not parsed as enums.

## Run

From this exercise directory:

```powershell
dotnet run --project .\RecordingWithXmlDefinedGuards\RecordingWithXmlDefinedGuards.csproj
dotnet test .\RecordingWithXmlDefinedGuards.Tests\RecordingWithXmlDefinedGuards.Tests.csproj
dotnet test .\RecordingWithXmlDefinedGuards.slnx
```

Tests cover custom guard names, guard resolution, guard denial, lifecycle ordering, rejected requests, and malformed or invalid configuration. This remains a synchronous, single-threaded teaching example, not a general-purpose state-machine framework.
