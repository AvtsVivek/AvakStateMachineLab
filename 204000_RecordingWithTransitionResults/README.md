# 204000 - Recording with transition results

This self-contained .NET 10 exercise builds on `203000_RecordingWithGuards`. It keeps the same states, requests, Start guard, and lifecycle behavior, while changing the result of `TryHandle` from `bool` to `TransitionResult`.

## Why a richer result?

A boolean tells the caller only that the request did or did not transition. It cannot explain whether the request is undefined in the current state or a valid transition denied by policy.

| Result | Meaning | Example |
|---|---|---|
| `Transitioned` | A defined request passed its guard if needed and lifecycle work completed. | Recording + Pause |
| `InvalidRequest` | The active state has no transition for this request. | Idle + Pause |
| `GuardDenied` | The transition exists, but its guard refused permission. | Idle + Start while permission is false |

Exceptions are not converted to result values. If the guard or lifecycle callback throws, that exception propagates. This distinction keeps an ordinary rejection separate from an operational failure.

## Transition rules

| Current state | Request | Guard | Next state / result |
|---|---|---|---|
| Idle | Start | Start permission | Recording / `Transitioned`, or Idle / `GuardDenied` |
| Recording | Pause | None | Paused / `Transitioned` |
| Recording | Stop | None | Idle / `Transitioned` |
| Paused | Resume | None | Recording / `Transitioned` |
| Paused | Stop | None | Idle / `Transitioned` |
| Any other pair | Any | None | Unchanged / `InvalidRequest` |

The Start permission is evaluated only for Start in Idle, on every attempt. Resume is not subject to it. Rejections run no lifecycle hooks and leave the mode unchanged.

## Design

- `TransitionResult` is a small public enum returned by `Recorder.TryHandle`.
- Each state owns the logic for determining whether a request is invalid, guard-denied, or transitioned.
- `Recorder` coordinates lifecycle order: exit source, replace active state, enter destination.
- `RecordingMode` is derived from the active state object and is not an independent state store.
- The permission guard and lifecycle reporting callback remain separate dependencies with different purposes.

This result enum is deliberately small and specific. It does not include exception values, rejection messages, arbitrary metadata, or a general diagnostics framework. A different application might use a result object if it needs additional details, but that is not necessary here.

## Run

Open `RecordingWithTransitionResults.slnx` in Visual Studio, or run these commands from this exercise directory:

```powershell
dotnet run --project .\RecordingWithTransitionResults\RecordingWithTransitionResults.csproj
dotnet test .\RecordingWithTransitionResults.Tests\RecordingWithTransitionResults.Tests.csproj
dotnet test .\RecordingWithTransitionResults.slnx
```

Expected output:

```text
Enter Idle
Start (permission denied): GuardDenied; state: Idle
Pause (invalid in Idle): InvalidRequest; state: Idle
Exit Idle
Enter Recording
Start (permission granted): Transitioned; state: Recording
Exit Recording
Enter Paused
Pause: Transitioned; state: Paused
Exit Paused
Enter Recording
Resume (Start permission is false): Transitioned; state: Recording
Exit Recording
Enter Idle
Stop: Transitioned; state: Idle
```

## Tests

The deterministic MSTest suite verifies all five accepted transitions, all seven invalid state/request pairs, guard denial, permission retries, Resume independent of Start permission, exact lifecycle ordering, state visibility during callbacks, guard and lifecycle exception propagation, nested-request rejection, null dependencies, and recorder independence. Result assertions are paired with mode and activity assertions so the enum value is checked against actual behavior.

## Deliberate limits

This remains synchronous learning code, not a production recording system or general-purpose state-machine library. There are no transition objects, hierarchy, asynchronous guards, resource reservation, thread-safety guarantees, rollback, or structured diagnostic reasons beyond the three result values. In particular, permission changes after a guard succeeds are outside this single-threaded example's concern.
