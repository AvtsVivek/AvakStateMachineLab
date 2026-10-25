# 203000 - Recording with guards

This self-contained .NET 10 exercise extends `202000_RecordingWithEntryAndExit` with one transition-specific permission check. Earlier exercises remain unchanged.

## Goal

Learn the difference between a request being valid for a state and the transition being permitted right now. Keep policy checks separate from lifecycle actions.

## Transition rules

| Current state | Request | Guard | Next state |
|---|---|---|---|
| Idle | Start | `CanStartRecording()` must return true | Recording |
| Recording | Pause | None | Paused |
| Recording | Stop | None | Idle |
| Paused | Resume | None | Recording |
| Paused | Stop | None | Idle |

The other seven state/request pairs reject without checking permission. A denied Start also returns false, stays Idle, and runs neither Exit nor Enter. Permission is evaluated once per Start attempt in Idle, never cached, and never evaluated during construction. Resume deliberately works even when permission to start a new recording is false.

## Design

- `Recorder` owns the active `IRecorderState`, prevents nested requests, and coordinates lifecycle actions.
- `IdleState` checks the request first and then calls `recorder.CanStartRecording()` before requesting a transition.
- `RecordingState` and `PausedState` preserve the earlier exercise's behavior.
- `Func<bool> canStartRecording` supplies application policy. The demonstration captures a mutable permission variable so a retry can be studied without hardware or external services.
- `Action<string> reportActivity` makes lifecycle execution observable. The console uses `Console.WriteLine`; tests use in-memory lists. This callback is not the permission check.
- `RecordingMode` remains an observation derived from the state object, not a second state store.

For an accepted Start, the order is **guard -> Exit Idle -> replace state -> Enter Recording**. Exit observes the source mode; entry observes the target mode. Construction enters Idle once without consulting the Start guard.

### Is this the same as CanEnter / CanExit?

They are related permission checks, but their scope differs. A coordinator can check source `CanExit` and destination `CanEnter` for every transition. A transition guard instead restricts a particular source/request/destination combination.

Putting our permission in a general Recording `CanEnter` check could restrict both Start and Resume. This exercise intentionally restricts only Start from Idle, so it uses a transition-specific guard. No transition class or generic state-machine engine is required. See section 15 of the root `Discussion.md` for the comparison with the existing application studied earlier.

## Run

Open `RecordingWithGuards.slnx` in Visual Studio, or run these commands from this exercise directory:

```powershell
dotnet run --project .\RecordingWithGuards\RecordingWithGuards.csproj
dotnet test .\RecordingWithGuards.Tests\RecordingWithGuards.Tests.csproj
dotnet test .\RecordingWithGuards.slnx
```

Expected console output:

```text
Enter Idle
Start permission: False
Start: rejected; state: Idle
Start permission: True
Exit Idle
Enter Recording
Start: accepted; state: Recording
Exit Recording
Enter Paused
Pause: accepted; state: Paused
Start permission: False
Exit Paused
Enter Recording
Resume: accepted; state: Recording
Exit Recording
Enter Idle
Stop: accepted; state: Idle
```

The denied Start has no lifecycle lines. Resume still succeeds after Start permission becomes false.

## Tests

The 24 deterministic MSTest cases cover initial entry, all five accepted pairs, all seven invalid pairs, guard denial, fresh permission on retry and after returning to Idle, Resume with permission denied, exact guard/lifecycle order and active modes, independent instances, null dependencies, propagated guard and lifecycle exceptions, and nested request rejection. Assertions inspect actual recorder behavior and exact in-memory traces, not console output.

## Deliberate limits

This is synchronous, single-threaded learning code, not a recording implementation or a rules engine. Permission checks should be side-effect-free and must not send requests back into the recorder. Entry and exit callbacks must not send nested requests either; such requests throw `InvalidOperationException`.

`TryHandle` returns false for either an invalid pair or a denied guard; it does not provide a rejection reason. Exceptions are not converted into false: a guard exception propagates before exit and leaves Idle active. Lifecycle exceptions propagate without rollback: exit failure leaves the source active, while entry failure leaves the target active. The processing flag resets after success, rejection, or exception, permitting later requests.

A permission check does not reserve resources or prevent another thread from changing external conditions. Concurrency, asynchronous policies, recovery, state-wide CanEnter/CanExit hooks, and hierarchical states are outside this exercise.
