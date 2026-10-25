# 201000 - Recording with the State pattern

## Goal

Implement the same recorder behavior as `200100_RecordingWithSwitchAndTests`, but use the State design pattern. Comparing these exercises shows how request handling changes when behavior is delegated to objects representing the current state.

This original learning example does not record audio or video.

## Projects

- `RecordingWithStatePattern`: .NET 10 console app containing the recorder and state objects.
- `RecordingWithStatePattern.Tests`: MSTest project that references the console project.

## Behavior

A recorder starts in `Idle` and supports these transitions:

| Current state | Request | Next state |
|---|---|---|
| Idle | Start | Recording |
| Recording | Pause | Paused |
| Recording | Stop | Idle |
| Paused | Resume | Recording |
| Paused | Stop | Idle |

The other seven state/request combinations are rejected and leave the current state unchanged. The tests cover the initial state, all twelve combinations, and independence between recorder instances.

## Run

From this exercise directory:

```powershell
dotnet test .\RecordingWithStatePattern.slnx
dotnet run --project .\RecordingWithStatePattern\RecordingWithStatePattern.csproj
```

Open `RecordingWithStatePattern.slnx` in Visual Studio to browse the implementation and run the tests.

## Read the code

- `Recorder.cs` is the context. It owns the active state object and delegates each request to it.
- `IRecorderState.cs` defines the behavior each state provides.
- `IdleState.cs`, `RecordingState.cs`, and `PausedState.cs` decide how requests behave in their respective modes and select the next state object when a request is accepted.
- `RecordingMode` is the externally readable mode; the active state object, not the enum, controls request handling.

Unlike the switch exercise, there is no central switch mapping every state/request pair. Each concrete state handles the requests relevant to that state and rejects the rest. This exercise intentionally has no UI, XML, hardware, asynchronous work, or guards.
