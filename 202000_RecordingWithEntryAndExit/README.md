# 202000 - Recording with entry and exit

## Goal

Extend the State-pattern recorder from `201000_RecordingWithStatePattern` with lifecycle actions. The previous examples remain unchanged. This synchronous example does not record audio or video.

## Projects

- `RecordingWithEntryAndExit`: .NET 10 console app and recorder implementation.
- `RecordingWithEntryAndExit.Tests`: MSTest tests referencing the console project.

## Transition rules

| Current state | Trigger/request | Next state |
|---|---|---|
| Idle | Start | Recording |
| Recording | Pause | Paused |
| Recording | Stop | Idle |
| Paused | Resume | Recording |
| Paused | Stop | Idle |

The other seven combinations are rejected without changing state or executing entry/exit actions.

## Lifecycle contract

Construction immediately runs `Idle.Enter` once. An accepted transition always performs:

```text
Source.Exit()          CurrentState still reports the source
Replace active object
Destination.Enter()    CurrentState now reports the destination
```

For example, Pause while recording reports exactly:

```text
Exit Recording
Enter Paused
```

Returning to a previously visited state runs its entry action again. Entry and exit belong to states; the recorder coordinates their ordering centrally in `TransitionTo`. Individual states still decide which requests are allowed. `RecordingMode` remains a public description derived from the active object, not the mechanism that controls behavior.

## An in-memory execution trace

The recorder takes an `Action<string>` callback. Each state's lifecycle methods invoke it. The console passes `Console.WriteLine`; tests pass `activities.Add` on a `List<string>`. The recorder does not depend on console output or know whether the callback stores the messages.

```csharp
List<string> activities = [];
Recorder recorder = new(activities.Add); // Records Enter Idle.
recorder.TryHandle(RecordingRequest.Start);
activities.Clear(); // Isolate the next request from construction and setup.

bool accepted = recorder.TryHandle(RecordingRequest.Pause);
// accepted is true; activities contains Exit Recording followed by Enter Paused.
```

Sequence assertions check exact order and count, not just whether messages appear. A missing, duplicated, reversed, or unexpected lifecycle call fails the assertion. These entries come from the actual state hooks, not from the test writing the expected messages into the log.

Messages alone do not prove when the active object changes. The accepted-transition tests also read `CurrentState` inside each callback: exit must observe the source mode, and entry must observe the destination. Construction invokes the initial entry before the constructor returns, so tests check its activity separately rather than trying to read an as-yet-unassigned recorder variable.

## Is the Action parameter only for testing?

Its main purpose in this exercise is observability, both for tests and for the console demonstration. It is not required by the State pattern or by entry/exit hooks.

```csharp
public void Enter(Action<string> reportActivity)
    => reportActivity("Enter Idle");
```

`Enter` is the lifecycle action. `reportActivity` is a reporting callback invoked by that action, not the action itself. The console uses it to display the sequence; tests use it to capture a sequence for assertions. This keeps console output out of the state implementations.

Real entry/exit methods could instead subscribe and unsubscribe from events, activate state-specific resources, or release those resources. They can be parameterless `Enter()` and `Exit()` methods, with dependencies supplied through constructors or an appropriate context. Passing a callback as a method parameter is a simple choice for this learning exercise, not a framework design requirement. Reporting could also be provided through another dependency if needed.

The current hook bodies are instrumentation rather than realistic recording behavior: the tests verify when hooks run and which state is active, not that hardware or recording resources were activated or released. In the application studied earlier, entry and exit methods perform actual work such as managing event subscriptions and UI state.

**Entry/exit hooks implement lifecycle behavior. The reporting delegate lets us observe that behavior.**

## Tests

The 19 cases cover:

- Initial Idle entry exactly once.
- All five accepted transitions, including lifecycle ordering and the mode visible during each hook.
- All seven rejected pairs, with no lifecycle activity.
- A complete Start/Pause/Resume/Stop sequence and repeated entry on revisits.
- Independent modes and activity logs for separate instances.
- Null callback validation, callback failures, and nested-request rejection.

## Run

Requires .NET 10 or a compatible newer SDK. From this directory:

```powershell
dotnet test .\RecordingWithEntryAndExit.slnx
dotnet run --project .\RecordingWithEntryAndExit\RecordingWithEntryAndExit.csproj
```

Alternatively, open `RecordingWithEntryAndExit.slnx` in Visual Studio and use Test Explorer.

## Scope and callback limitations

The callback is synchronous and must not send requests back into the same recorder. Nested requests throw `InvalidOperationException` rather than interleaving transitions. This small safety check is not a domain transition guard or an event queue. The recorder is not thread-safe.

Callback exceptions propagate; there is no rollback. An exit failure leaves the source active, while an entry failure leaves the destination active. This example does not provide recovery guarantees for real hardware or partially completed actions. Construction fails if the initial entry callback throws. These limitations are explicit rather than hidden by catches or success-shaped results.

There are no domain guards, asynchronous operations, external logging dependencies, UI, or hardware. Start with `Recorder.TransitionTo`, then read the lifecycle methods in each state and `RecorderLifecycleTests`.
