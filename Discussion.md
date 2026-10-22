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

## 7. Next learning steps

1. Walk through `RecordingState.cs` and `RecordingRequest.cs`.
2. Read `Recorder.cs` and trace how each current-state/request pair selects a transition.
3. Add a dedicated test project and verify accepted transitions, rejected requests, and independent recorder instances.
4. Create a later exercise implementing the same behavior with the State design pattern, then compare the two approaches.
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
