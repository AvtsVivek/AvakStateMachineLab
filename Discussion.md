# Discussion so far

This document captures the decisions and progress from our conversation about restarting the state-machine learning project. It is a recap, not a verbatim chat transcript.

## 1. Reviewing the original prototype

The original `AvakStateMachineLab` prototype was reviewed to understand its implementation and direction. It used XML state definitions, reflection-based state creation and dependency resolution, mutable state transitions, UI/view-model integration, and experiments with nested state files.

The assessment was that it is valuable as a prototype and requirements reference, but would benefit from a smaller restart with clearer boundaries. In particular:

- Keep the state-machine core independent from XML parsing and UI frameworks.
- Separate state-machine definitions from individual running instances.
- Specify transition and lifecycle behavior before adding advanced features.
- Treat hierarchy, parallel states, history, and Back behavior as later capabilities that need clear requirements.
- Preserve the original prototype as a reference rather than extending it as the new implementation.

The original core test project built successfully, and one basic transition test passed. The console sample had a build failure due to missing interface members. No files in the original prototype were changed.

## 2. Studying the existing application's state usage

The project's state implementation was studied to help clarify the learning goal. It contains more than one style:

- A hierarchical, XML-configured application-state system used for interaction modes, navigation, global transitions, history, and parallel states.
- A separate recording lifecycle implementation that directly uses state objects to handle requests such as start, pause, resume, and stop.

This helped distinguish:

- **UI/navigation states:** describe the current interaction mode, visible content, and applicable navigation behavior.
- **Business/device lifecycle states:** describe the condition of a process or resource, such as recording or a device connection.
- **A general-purpose hierarchical state machine:** describes reusable engine capabilities that can represent either kind of behavior.

The State design pattern and a table-driven state machine are related but distinct. The State pattern delegates behavior to the current state object. A table-driven machine represents transitions separately as rules mapping a state and trigger to a destination.

The conclusion was that the learning project does not need to choose a final framework purpose immediately. It can start with a small example and let requirements emerge through comparison.

## 3. Repository naming and license

The new repository is `AvakStateMachineLab`, cloned at:

`C:\Trials\AvakStateMachineLab`

The repository name follows the user's existing no-dot repository naming style. Dotted names can still be used for .NET projects and namespaces.

MIT was recommended as a simple, widely used license for the learning repository. Only code that the user has rights to publish should be included; the new examples should be original rather than copied from the other application's source.

## 4. Exercise structure

The user described a preference for numbered, self-contained learning examples, ordered from simpler to more complex, with six-digit numeric prefixes and gaps for future examples.

The agreed approach is to keep each exercise in its own folder with its own solution and README. The number is a suggested learning order, not a strict complexity measurement.

The first exercise selected is:

`200000_RecordingWithSwitch`

Starting with a switch-based implementation gives us a baseline to compare against a later State-pattern implementation.

## 5. First exercise: Recording with a switch

Created under `200000_RecordingWithSwitch`:

- `RecordingWithSwitch.slnx` — standalone solution for the exercise.
- `RecordingWithSwitch\RecordingWithSwitch.csproj` — .NET 10 console project.
- `RecordingWithSwitch\RecordingState.cs` — `Idle`, `Recording`, and `Paused` states.
- `RecordingWithSwitch\RecordingRequest.cs` — `Start`, `Pause`, `Resume`, and `Stop` requests.
- `RecordingWithSwitch\Recorder.cs` — switch-based transition rules and a `TryHandle` result indicating whether a request was accepted.
- `RecordingWithSwitch\Program.cs` — a fixed demonstration sequence.
- `README.md` — rules, explanation, and run instructions.

The root `README.md` now lists this exercise.

### Behavior

| Current state | Request | Resulting state |
|---|---|---|
| Idle | Start | Recording |
| Recording | Pause | Paused |
| Recording | Stop | Idle |
| Paused | Resume | Recording |
| Paused | Stop | Idle |

All other requests are rejected and leave the current state unchanged. A recorder starts in `Idle`, exposes its current state as read-only, and owns its state independently.

This is a learning example only. It does not record audio or video and has no UI, XML, asynchronous processing, or state subclasses.

## 6. Verification and Visual Studio

The new solution built successfully using the .NET CLI. The demonstration output was checked against the exact sequence documented in the exercise README, including a rejected `Pause` request while idle.

Visual Studio initially had the original prototype open. The new exercise solution was then opened in the running Visual Studio instance, replacing the original solution. The loaded project was confirmed as:

`200000_RecordingWithSwitch\RecordingWithSwitch\RecordingWithSwitch.csproj`

No commits were created.

## 7. Learning progress and next steps

1. Walk through `RecordingState.cs` and `RecordingRequest.cs`.
2. Read `Recorder.cs` and trace how each current-state/request pair selects a transition.
3. The tested switch-based checkpoint is now `200100_RecordingWithSwitchAndTests`; it verifies accepted transitions, rejected requests, initial state, and independent recorder instances.
4. `201000_RecordingWithStatePattern` now contains a State-pattern implementation with the same observable behavior and a matching test matrix. Compare where each design puts its transition logic.
5. Use one concrete navigation flow from the existing application as a later study exercise; introduce hierarchy or parallel states only when their behavior is understood and needed.

## 8. Requests, triggers, commands, and events

The user commonly uses the term **trigger**. In this exercise, a request also acts as a trigger, but the terms emphasize different things.

- **Request:** asks the system to do something, such as pause recording. It expresses intent and can be rejected.
- **Trigger:** the input or occurrence that prompts the state machine to evaluate a transition. It can come from a request, a button press, a timer, or a device notification. It does not guarantee a state change.
- **Command:** expresses an instruction or intent, such as `PauseRecording`. For this example, a command and a request describe the same kind of input.
- **Event:** reports something that has happened, such as `RecordingPaused` or `DeviceDisconnected`. An event can itself trigger another transition.
- **Transition:** the change from one state to another when the applicable rules allow it. It is not the request or trigger itself.

### Example: the same trigger in different states

| Current state | Request / trigger | Outcome |
|---|---|---|
| Recording | Pause | Accepted; transition to Paused |
| Idle | Pause | Rejected; remain Idle |

In both rows, `Pause` is a request and is used as the trigger input. Only the first row produces a transition. The machine considers its current state and any applicable conditions before accepting the input.

A useful mental model is:

```text
Current state + trigger + applicable conditions
                    |
                    v
          Evaluate transition rules
                    |
          +---------+---------+
          |                   |
       Accepted            Rejected
          |                   |
   Change state         State unchanged
```

### Intent versus notification

`PauseRecording` means **please pause**. `RecordingPaused` means **pausing has happened**. These should not be confused in a real asynchronous recorder: requesting a pause and receiving confirmation may be separate steps. The first exercise intentionally treats accepted operations as instantaneous.

The distinction is about meaning, not just naming. A framework may use the word "event" for all incoming messages, including command-like inputs; always check its terminology.

### Terminology for this exercise

`RecordingRequest` is appropriate because its values ask the recorder to perform operations, and `TryHandle` explicitly reports acceptance or rejection. Calling these inputs triggers is also reasonable when discussing the state machine. The existing code has not been renamed.

The state graph in `200000_RecordingWithSwitch\recording-state-graph.svg` illustrates accepted transitions and rejected requests. Its dashed loops mean **no transition occurred**; they are not accepted self-transitions that exit and re-enter the same state.

## 9. Where rules engines fit

A state machine and a rules engine are related, but have different primary responsibilities:

- **State pattern:** organizes state-dependent behavior into objects.
- **State machine:** defines and executes valid progression between states over time.
- **Rules engine:** evaluates conditions and policies against facts, potentially executing actions.

### State progression versus policy evaluation

Our recorder's state machine answers questions such as:

- What state is the recorder in?
- Does this trigger have a transition from the current state?
- Which state comes next?
- What entry and exit behavior should execute?

A real recording policy might instead consider available disk space, device readiness, user permission, license validity, and temperature. These facts determine whether starting a recording is permitted, rather than defining the recording lifecycle itself.

| Question | Primary responsibility |
|---|---|
| Can Pause take us from Recording to Paused? | State machine |
| Is this user permitted to record? | Policy/rule evaluation |
| Is sufficient storage available? | Policy/rule evaluation |
| What happens when we enter Recording? | State-machine action |
| Which discount applies to this customer? | Rules engine |
| Where should Back navigation return? | Navigation/history logic |

### Combining them through transition guards

A **guard** is a condition that must pass before a transition is accepted:

```text
Source state + trigger + guard -> target state

Idle + Start + CanStartRecording -> Recording
```

`CanStartRecording` could be a Boolean expression, an application method, a policy service, or a decision produced by a rules engine.

```text
Start trigger
     |
     v
State machine finds Idle -> Recording
     |
     v
Evaluate guard using current facts
     |
     +-- Not permitted --> Reject; remain Idle
     |
     +-- Permitted ------> Execute the transition
```

**A guard does not require a rules engine.** A simple method is usually sufficient until policies become numerous, complex, or independently maintained.

### Microsoft RulesEngine

[Microsoft RulesEngine](https://github.com/microsoft/RulesEngine) separates business rules and policies from core application logic.

- Rules can be represented in JSON or created programmatically.
- Expressions are evaluated against supplied inputs.
- Evaluation returns results indicating which rules passed or failed.
- It also supports actions, so it is not limited to returning Boolean decisions.

A possible use is evaluating recording eligibility and identifying which conditions failed.

**Terminology caution:** a named collection of rules is called a workflow. This does not, by itself, mean a persistent process moving through states such as Idle, Recording, and Paused.

### NRules

[NRules](https://github.com/NRules/NRules) is a production rules engine based on the Rete matching algorithm. Rules are authored using a C# domain-specific language (DSL).

A production rule is a condition/action rule:

```text
WHEN matching facts exist
THEN execute an action
```

NRules maintains facts in a session and matches rules against them. Actions can insert or update facts, making other rules applicable. This supports **forward chaining**: one rule's effects can lead to another rule firing.

For example:

```text
Facts:
    Storage is low
    Recorder is recording

Rule:
    When both facts match,
    request that recording stop
```

A useful boundary is for the rule to send a Stop trigger, leaving the state machine responsible for executing the valid transition.

### Can a rules engine replace a state machine?

A rules engine can encode transitions, for example:

```text
WHEN state is Recording AND request is Pause
THEN set state to Paused
```

However, the application must still define which rule wins when several match, whether multiple transitions can execute for one input, entry and exit ordering, concurrent-input handling, action-failure behavior, hierarchy, and history. This amounts to implementing state-machine semantics on top of the rules engine.

Conversely, putting every policy directly in state classes can make those classes difficult to maintain. Keeping lifecycle progression and policy evaluation separate can help when the application grows.

### Implication for our exercises

The current switch contains transition rules; the word "rules" does not mean a rules-engine library is needed. Do not introduce either library into the first exercise.

The libraries above are references for future study, not dependencies or architecture decisions for this repository.

## 10. Defining transitions and placing guards

A **transition** is a rule that says what the machine may do when a particular trigger arrives in a particular state. A useful general form is:

```text
Source state + Trigger [optional guard] -> Target state
```

For the recorder:

```text
Idle + Start -> Recording
Recording + Pause -> Paused
Recording + Stop -> Idle
Paused + Resume -> Recording
Paused + Stop -> Idle
```

For a guarded transition:

```text
Idle + Start [CanStartRecording] -> Recording
```

This means: when the recorder is `Idle` and receives `Start`, move to `Recording` only if `CanStartRecording` is true. If the guard is false, the transition is not taken and the recorder remains `Idle`.

### Guard responsibilities

A guard belongs to a **candidate transition**: it decides whether that transition is allowed given the current facts. The responsibilities are:

- **State machine:** identifies the candidate transition and evaluates its guard.
- **Guard:** answers whether this transition is allowed right now.
- **Policy/domain logic:** supplies the reasoned decision, such as whether permission, device readiness, and storage requirements are satisfied.
- **Transition action:** performs work associated with the transition after it is accepted.

For a small example, the guard can be a condition in the state's switch case. As policy grows, it can become a named method or an injected policy service. Do not put side effects in a guard: it should decide, not start the recorder.

The guard answers **"may we start?"**, not **"did the hardware successfully start?"** If starting hardware is asynchronous and can fail, use a `Starting` state and later success/failure triggers. Do not treat permission to start as proof that starting succeeded.

### Mapping this to the current code

In `Recorder.TryHandle`, each matching `case` represents a transition. For example:

```csharp
case RecordingState.Idle when request == RecordingRequest.Start:
    CurrentState = RecordingState.Recording;
    return true;
```

The case condition matches the current state and trigger; assigning `CurrentState` performs the transition. The `default` case rejects requests for which no transition is defined. The example currently has no separate domain guard: it accepts `Start` whenever the recorder is idle.

As the model grows, keep the guard distinct from the transition action: the guard decides whether the transition may happen; the action performs associated work. Entry and exit behavior are related lifecycle behavior, but do not have to be part of the transition rule itself.

## 11. Second exercise: Recording with a switch and tests

Created the self-contained `200100_RecordingWithSwitchAndTests` checkpoint to keep the switch-based baseline intact while adding automated behavioral coverage.

- `RecordingWithSwitchAndTests.slnx` is the exercise solution.
- `RecordingWithSwitch` is the .NET 10 console project containing the recorder and demonstration.
- `RecordingWithSwitch.Tests` is the MSTest project and references the recorder project.
- The exercise README documents the behavior and commands.

The 14 test cases cover the initial `Idle` state, all five allowed transitions, all seven rejected state/request pairs, and independence of separate recorder instances. The production transition logic remains the same switch-based design as the first exercise.

The solution is open in Visual Studio. The solution build succeeded, and both direct project testing and solution-level `dotnet test` passed all 14 tests. Visual Studio Test Explorer also discovered and passed all 14 tests.

The next planned implementation exercise is `210000_RecordingWithStatePattern`, using the same behavior so the two designs can be compared.
