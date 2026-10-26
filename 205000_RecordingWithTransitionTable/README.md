# 205000 - Recording with a transition table

This self-contained .NET 10 exercise preserves the behavior of `204000_RecordingWithTransitionResults` while moving the transition rules from individual state objects into a centralized table.

## Transition table

| Source | Request | Target | Guard |
|---|---|---|---|
| Idle | Start | Recording | Start permission |
| Recording | Pause | Paused | None |
| Recording | Stop | Idle | None |
| Paused | Resume | Recording | None |
| Paused | Stop | Idle | None |

Any source/request pair without a row returns `InvalidRequest`. If the Start permission row is present but permission is refused, the result is `GuardDenied`. Every accepted row returns `Transitioned` after the lifecycle sequence completes.

The guard is attached only to **Idle + Start**. Resume remains allowed even if permission to start a new recording is false. Rejections run no exit or entry hooks and leave the current state unchanged.

## Design

- `TransitionTable` maps `(source mode, request)` to a `TransitionDefinition` containing target mode and guard metadata.
- `Recorder.TryHandle` performs table lookup, evaluates the guard if the row requires it, creates the target lifecycle state, and coordinates the transition.
- `IdleState`, `RecordingState`, and `PausedState` implement entry/exit behavior only; they do not decide which requests are valid.
- `StateFactory` maps a target mode to its lifecycle state object. Its small switch is construction logic, not transition selection.
- `TransitionResult` keeps the same three outcomes as the previous exercise: `Transitioned`, `InvalidRequest`, and `GuardDenied`.

For each accepted transition, execution is **table lookup -> optional guard -> Exit source -> replace state -> Enter target**. The active mode observed during Exit is the source; during Enter it is the target. Guard exceptions happen before lifecycle work and propagate. Lifecycle exceptions retain the same no-rollback behavior as the prior exercises.

## Compare with state-owned rules

In `204000_RecordingWithTransitionResults`, each state class decides which requests it accepts and chooses the destination. Here, the table makes those decisions visible as data in one place. Both approaches implement the same five accepted transitions, seven invalid pairs, guard scope, result values, and lifecycle ordering.

A table is useful when a compact set of rules should be inspected or edited together. As conditions, priorities, hierarchy, dynamic targets, and actions grow, table dispatch can become complicated. This example uses a small table and a focused recorder; it does not add a generic engine. See section 17 of the root `Discussion.md` for the comparison and tradeoffs.

## Run

Open `RecordingWithTransitionTable.slnx` in Visual Studio, or run these commands from this exercise directory:

```powershell
dotnet run --project .\RecordingWithTransitionTable\RecordingWithTransitionTable.csproj
dotnet test .\RecordingWithTransitionTable.Tests\RecordingWithTransitionTable.Tests.csproj
dotnet test .\RecordingWithTransitionTable.slnx
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

The 25 deterministic MSTest cases cover initial entry, every table row, every missing known state/request pair, an unknown numeric request, guard denial and retry, guard scope, exact guard/lifecycle order and observed modes, nested requests, exception propagation, null dependencies, and instance independence. Assertions check the typed result together with the resulting mode and exact in-memory activity trace.

## Deliberate limits

This is synchronous learning code, not a general-purpose state-machine library. The table is fixed at initialization and has one optional boolean guard requirement. There are no dynamic rules, priorities, transition actions, hierarchy, async policies, thread-safety guarantees, or rollback. The state factory is an explicit mapping between modes and lifecycle objects; it is kept separate from the table so each has one clear responsibility.
