# 211000 - Recording with XML transitions

This standalone .NET 10 exercise builds on [210000's cached states](../210000_RecordingWithCachedStates/README.md) by moving the transition graph and initial state from C# into one XML file. The state classes, cached state objects, transition guard, state permissions, lifecycle callbacks, and transition results remain in C#. Earlier exercises remain unchanged.

## What the XML owns

`RecordingWithXmlTransitions\recorder-transitions.xml` declares the initial state, valid state/request pairs, their targets, and the Start-only transition guard:

```xml
<recorder initial="Idle">
  <state name="Idle">
	<transition trigger="Start" target="Recording" guard="StartPermission" />
  </state>
  <state name="Recording">
	<transition trigger="Pause" target="Paused" />
	<transition trigger="Stop" target="Idle" />
  </state>
  <state name="Paused">
	<transition trigger="Resume" target="Recording" />
	<transition trigger="Stop" target="Idle" />
  </state>
</recorder>
```

The runtime resolves XML names to the existing `RecordingMode` and `RecordingRequest` enum values. It does not use reflection or construct classes named by the XML. `Recorder` still owns dispatch, permission checks, lifecycle calls, cached states, and exception behavior. The `StartPermission` guard delegates to the injected callback; state-wide `CanEnter` and `CanExit` behavior remains in the state objects.

This is deliberately a small flat configuration format, not a general XML state-machine language. It has one file, three explicitly supported states, four trigger names, and one supported transition guard. It has no hierarchical parent/child states or parallel regions. XML cannot specify executable code or select C# classes or assemblies for the runtime to instantiate.

## Configuration validation

`TransitionTable.Load` reads the XML, then calls its separate `ValidateConfiguration` method to check the document structure, names, and transition references before building the table. The recorder receives the table only after validation succeeds. Malformed XML is reported by `XmlReader`/`XDocument`; well-formed XML that violates the configuration contract produces a `FormatException`, including:

- Invalid or undeclared initial states.
- Unknown state, trigger, or target names.
- Missing or empty required attributes.
- Targets that are known enum names but have no state declaration.
- Duplicate state declarations or source/trigger transitions.
- Unsupported attributes, elements, or guards.
- Numeric enum aliases, so XML must use the exact named values.
- Child elements inside a transition. For example, `<action />` is not a supported action feature; it is only an example of an unsupported child element that the loader rejects. XML does not execute code.

The loader prohibits DTD processing and does not resolve external XML resources.

## What this example demonstrates

- Configuration can describe transition choices without moving state behavior out of C#.
- XML names are parsed into a constrained, typed model before requests are handled.
- Invalid configuration is rejected before the state machine begins.
- A data-driven transition table can be tested independently from state lifecycle behavior.

Compare this flat per-recorder XML mapping with ExecUI's application and substate XML tables in the notes from that investigation. This lesson intentionally does not copy ExecUI's assembly-based resource loading, cross-module resolution, hierarchy, global transitions, or parallel-state support.

## Run

Open `RecordingWithXmlTransitions.slnx`, or run from this exercise directory:

```powershell
dotnet run --project .\RecordingWithXmlTransitions\RecordingWithXmlTransitions.csproj
dotnet test .\RecordingWithXmlTransitions.Tests\RecordingWithXmlTransitions.Tests.csproj
dotnet test .\RecordingWithXmlTransitions.slnx
```

The console walkthrough preserves the behavior of 210000. The tests verify the configured initial state, a complete transition sequence and lifecycle trace, guard and state-permission denials, invalid requests, and malformed or invalid configuration cases.

## Boundaries

This remains a teaching example, not the general-purpose framework in `C:\Trials\AvksStateMachine`. It is synchronous and single-threaded, flat, and intentionally explicit about the small set of supported state and trigger names. It does not model async operations, hierarchy, rollback, schema versioning, hot reload, or concurrent event processing. Add those only when a concrete use case requires and defines their semantics.
