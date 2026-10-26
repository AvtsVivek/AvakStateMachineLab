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

The State-pattern implementation is now in `201000_RecordingWithStatePattern`, using the same behavior so the two designs can be compared.

## 12. Why keep RecordingMode alongside state classes?

The State-pattern exercise has a one-to-one mapping between its concrete state classes and the public enum:

| Behavioral state object | Public mode |
|---|---|
| `IdleState` | `RecordingMode.Idle` |
| `RecordingState` | `RecordingMode.Recording` |
| `PausedState` | `RecordingMode.Paused` |

**The enum is not required by the State pattern.** It is intentional duplication for observability: state objects implement behavior, while the enum describes the current mode to callers.

### Behavior versus observation

The recorder owns the actual state object:

```csharp
private IRecorderState _currentState = new IdleState();
```

Request handling is delegated to that object:

```csharp
return _currentState.TryHandle(this, request);
```

The public property reports a value derived from that same object:

```csharp
public RecordingMode CurrentState => _currentState.Mode;
```

There are not two independently stored current states. The enum value is derived from the active state object, so no separate enum field needs to be synchronized. However, adding a new state may still require adding an enum member and its mapping. This is a real maintenance trade-off, and an incorrect mapping could misreport the mode.

### Purpose in this learning exercise

The enum gives the console and tests a simple, strongly typed observation without exposing the implementation objects:

```csharp
Assert.AreEqual(RecordingMode.Paused, recorder.CurrentState);
```

`IRecorderState` and its implementations remain internal. Callers can observe what the recorder is doing without receiving objects that implement its behavior. This also lets both recording exercises be checked against the same observable transition expectations without asserting concrete implementation types.

### Alternatives

- **Expose the state object:** callers could inspect its type, but this would expose implementation details. Making the current `IRecorderState` public would also expose its request-handling method; callers should normally send requests through `Recorder`. Do not expose this interface solely to remove the enum.
- **Expose a descriptive name:** useful for diagnostics and console output, but strings lose compile-time checking and are brittle if callers use them for application logic.
- **Do not expose the complete state:** some APIs provide operations, results, events, or observations such as `IsRecording` instead. This is reasonable when consumers do not need to know the full internal state model, although it makes this exercise less direct to inspect.

A richer design need not have a one-to-one mapping: several internal states could report the same public mode. That is a possible future use, not a requirement of this example.

### Naming and conclusion

`CurrentMode` would be clearer than `CurrentState` for the existing enum-valued property because it returns a public description rather than the active state object. This is a naming suggestion only; the code has not been renamed.

**The state object decides behavior. The enum reports the current mode.** Removing the enum would not remove the State pattern; we would simply choose another observation mechanism or expose no mode at all.

## 13. Entry, exit, and in-memory execution traces

`202000_RecordingWithEntryAndExit` extends the State-pattern example while preserving its five transitions and seven rejected pairs. Construction enters Idle once. Each accepted transition exits the source, replaces the active object, and enters the destination. Rejection runs neither hook.

The recorder accepts an `Action<string>` callback. Entry and exit methods report their activity through it. The console passes `Console.WriteLine`, but tests pass a list's `Add` method, avoiding console capture, shared console state, and presentation-dependent assertions.

For Pause while recording, the expected trace is exactly:

```text
Exit Recording
Enter Paused
```

An ordered sequence assertion detects missing, duplicate, reversed, or unexpected actions. Tests clear setup activity before checking an individual request. Separate tests check initial entry, revisits, rejected requests, and independent instances.

The two messages alone establish exit/entry order, not when state replacement occurred. The accepted-transition tests also read the public mode inside the callback and assert that exit observes the source while entry observes the destination.

This is a learning execution trace, not a production logging framework. Callbacks run synchronously. Nested requests are explicitly rejected, and callback exceptions propagate without rollback; exit failure leaves the source active and entry failure leaves the target active. There is no domain guard, thread-safety guarantee, or asynchronous recovery protocol. See the exercise README for run instructions and the full scope.

## 14. Why do Enter and Exit take an Action delegate?

The `Action<string>` parameter primarily makes lifecycle execution observable in this exercise. It supports both testing and the console demonstration; it is not required by the State pattern or by entry/exit methods.

```csharp
public void Enter(Action<string> reportActivity)
    => reportActivity("Enter Idle");
```

`Enter` is the lifecycle action; `reportActivity` reports that the action ran. It is not the entry action itself.

| Caller | Callback | Purpose |
|---|---|---|
| Console demonstration | `Console.WriteLine` | Display the lifecycle sequence |
| Tests | `activities.Add` | Capture the actual sequence for assertions |

Without hardware or UI dependencies, these activity messages give the hooks small, visible behavior so their execution order can be studied. Injecting the callback also avoids hard-coding console output into state classes.

Real lifecycle behavior could subscribe to events or activate state-specific resources on entry, then unsubscribe or release resources on exit. Methods may be parameterless `Enter()` and `Exit()`, with dependencies supplied through constructors or a context. The existing application studied earlier uses entry/exit methods for actual work such as event subscriptions and UI state changes.

The current hook bodies are instrumentation, not realistic recording operations. Tests establish when the hooks execute and the active mode during execution; they do not establish that recording resources were actually started or released.

Passing the reporting delegate as a method parameter was a simple educational choice. It could be supplied another way, and an application may not need a reporting callback at all. No API change is implied by this discussion.

**Entry/exit hooks implement lifecycle behavior. The reporting delegate lets us observe that behavior.**

## 15. Transition guards versus CanEnter and CanExit

A guard is a permission check performed before a transition's lifecycle work. It should answer whether an action is permitted, not perform the action itself. Returning false rejects the request without exit, state replacement, or entry.

`203000_RecordingWithGuards` injects a `Func<bool>` named `canStartRecording`. Only `Idle + Start -> Recording` evaluates it. The permission is checked afresh on each such request; it is not cached. Initial Idle entry, unrelated requests, and `Paused + Resume -> Recording` do not evaluate this guard.

For an allowed Start, execution is:

```text
Check Start permission while Idle
Exit Idle
Replace active state with Recording
Enter Recording
```

A denied Start leaves Idle active with no lifecycle activity. Changing permission allows a later retry. The boolean result deliberately does not distinguish an invalid state/request pair from a denied guard; richer rejection results are outside this lesson.

The existing application's `CanExit` and `CanEnter` methods serve a related purpose at a different scope. Its transition coordinator checks source `CanExit`, then destination `CanEnter`, before running exit and entry. Parent and parallel-state checks can contribute to that decision.

| Check | Scope |
|---|---|
| Transition guard | One source/request/destination combination, such as Idle + Start |
| Source CanExit | Whether the source may be left through the coordinator |
| Destination CanEnter | Whether the destination may be entered through the coordinator |

A general Recording `CanEnter` permission would also affect Resume when the coordinator checks it for every arrival. Our requirement intentionally restricts only starting a new recording, not continuing a paused one, so a Start-specific guard is clearer. These approaches can coexist, and a guard does not require a dedicated transition object; here IdleState checks it before asking the recorder to transition.

The permission delegate represents application policy, unlike the activity callback used for observation. A real application could obtain permission from an injected service. This exercise does not add a rules engine, hardware access, state-wide CanEnter/CanExit methods, or hierarchy. Policy exceptions propagate before lifecycle work and leave Idle active; nested requests from either callback are explicitly rejected. The previous lifecycle exception and no-rollback behavior remains unchanged.

## 16. Reporting transition outcomes

In `203000_RecordingWithGuards`, `TryHandle` returns `bool`. `false` means either that the request has no transition in the current state or that the Start guard denied it. When callers need to react differently to those cases, a boolean loses useful information.

`204000_RecordingWithTransitionResults` returns a small `TransitionResult` enum:

| Result | Meaning |
|---|---|
| `Transitioned` | The request was valid, any applicable guard passed, and lifecycle work completed. |
| `InvalidRequest` | There is no transition for this request in the current state. |
| `GuardDenied` | A transition exists, but its guard refused it. |

For example, Idle + Pause returns `InvalidRequest`, while Idle + Start with permission denied returns `GuardDenied`. Both leave Idle active and run no lifecycle hooks, but the result explains why. A successful Start returns `Transitioned` after exit and entry complete.

Exceptions remain exceptions: a throwing guard or lifecycle callback is not translated to one of the enum values. This avoids treating an operational failure as an ordinary domain rejection. The result also does not carry arbitrary text or diagnostic details; add a richer result object only when a real caller needs that information.

The result type does not centralize the rules. Each state still handles its own requests and returns the appropriate outcome, while the recorder continues coordinating exit, replacement, and entry. The new exercise preserves the transitions and guard scope so the result contract is the only conceptual change.

## 17. State-owned transitions versus a centralized transition table

`204000_RecordingWithTransitionResults` keeps transition decisions in state classes. Idle recognizes Start, checks permission, and chooses Recording; the other states handle their own requests. This keeps each state's behavior together, and the context focuses on shared coordination.

`205000_RecordingWithTransitionTable` moves the valid source/request/target combinations into a central table. For example, a row records `Idle + Start -> Recording` and marks that it requires the Start permission guard. State objects still own lifecycle hooks, and the recorder still coordinates exit, replacement, and entry. The table changes where transition rules are declared, not the transition semantics.

| State-owned rules | Centralized transition table |
|---|---|
| Each state handles requests it accepts. | Rows list source, request, target, and applicable guard. |
| State behavior and transition choices stay together. | The complete transition graph is easy to inspect in one place. |
| Adding behavior may involve modifying a specific state. | Adding transition metadata may be direct, but table dispatch and state construction also need care. |

Both approaches retain the same `TransitionResult` distinctions, five accepted transitions, seven invalid pairs, Start-only guard, and lifecycle ordering. The guard is metadata on just the Idle + Start row, not a general Recording-entry restriction, so Resume remains permitted when Start permission is false.

A table is useful when rules are small and benefit from being inspected as data. As complexity grows, conditions, priorities, hierarchy, dynamic targets, and actions can make the table's dispatch logic complicated. A table does not automatically make a state machine simpler, and it need not become a generic framework. Compare the exercise implementation with `204000` and notice which change is data organization versus behavior.
