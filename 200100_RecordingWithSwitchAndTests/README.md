# 200100 - Recording with a switch and tests

This checkpoint repeats the switch-based recorder from `200000_RecordingWithSwitch` and adds automated tests. Keeping this exercise self-contained lets us change or extend it without altering the earlier baseline.

## Projects

- `RecordingWithSwitch`: .NET 10 console demonstration and recorder implementation.
- `RecordingWithSwitch.Tests`: MSTest tests referencing the recorder project.

## Behavior under test

The recorder starts in `Idle` and supports these transitions:

| Current state | Trigger/request | Next state |
|---|---|---|
| Idle | Start | Recording |
| Recording | Pause | Paused |
| Recording | Stop | Idle |
| Paused | Resume | Recording |
| Paused | Stop | Idle |

Every other state/trigger pair is rejected, and rejection leaves the state unchanged. Tests cover every accepted and rejected pair, the initial state, and independence between recorder instances.

## Run

From this directory:

```powershell
dotnet test .\RecordingWithSwitchAndTests.slnx
dotnet run --project .\RecordingWithSwitch\RecordingWithSwitch.csproj
```

Open `RecordingWithSwitchAndTests.slnx` in Visual Studio to browse the two projects and run the test suite.

## Learning focus

The production code is intentionally unchanged in design: transition rules still live in a switch. The tests make its behavior explicit and provide a baseline for a later State-pattern implementation.
