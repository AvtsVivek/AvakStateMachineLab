# AvakStateMachineLab
A hands-on exploration of the State pattern and state machines in .NET.

## Learning approach

The exercises use a small recorder lifecycle to study one design choice at a time. They are original, standalone teaching examples: they do not record audio or video, depend on a UI or device, or attempt to be a general-purpose state-machine framework. The progression starts with a switch, then compares state objects, lifecycle hooks, guards, results, transition tables, hierarchy, state permissions, shared base behavior, cached state instances, XML-configured transitions, and source-aware trigger matching. Read neighboring exercises to see what changed and what stayed constant.

A state describes the situation the system is currently in. A transition is a permitted change from a source state to a target state, usually selected using both the current state and an incoming input. The State pattern puts state-dependent behavior in state objects; a table-driven design represents transition choices as data. These are related approaches, but they organize behavior differently.

The same concepts can apply to different problems: UI/navigation states describe interaction modes and destinations; business or device lifecycle states describe a process or resource; a general-purpose state machine is an engine intended to express multiple kinds of such models. This repository begins with a concrete lifecycle so requirements can be learned before considering a reusable engine.

### Requests, triggers, commands, events, and transitions

These terms are related, but not interchangeable in meaning:

| Term | Meaning in these exercises |
|---|---|
| Request or command | An input expressing intent, such as asking the recorder to pause. It can be rejected. |
| Trigger | The input or occurrence that causes the machine to evaluate transition rules. It might be a request, button press, timer, or device notification. |
| Event | A notification that something has happened, such as `RecordingPaused`. In an asynchronous system, a request to pause and confirmation that pausing completed are distinct. |
| Transition | The state change taken when the current state, input, and applicable conditions allow it. |

For example, `Pause` can be accepted in Recording and rejected in Idle. The same input does not guarantee a transition: the current state and applicable conditions matter. Frameworks sometimes use “event” broadly for all incoming messages, so use each framework's terminology in context. The recorder examples treat accepted operations as immediate; they do not model an asynchronous hardware confirmation.

### State machines, guards, and rules engines

A state machine answers which transitions are valid from the current state and performs the associated lifecycle behavior. A guard answers whether one candidate transition is permitted right now. A policy or rules engine can evaluate facts such as device readiness, available storage, user permission, or license validity to supply that decision. A simple method is enough for the exercises; a rules engine is not required just because a transition has a guard.

A guard decides; it should not perform the transition's side effects. Permission to start also does not prove that hardware successfully started. If starting can complete asynchronously or fail, a model may need a `Starting` state and later success/failure triggers.

A rules engine can express state-dependent rules, but then the application still has to define rule priority, whether multiple rules can fire, lifecycle ordering, concurrency, failures, hierarchy, and history. That is state-machine behavior the application would need to supply. Conversely, placing every business policy in state classes can couple policy evaluation to lifecycle progression. Keep those responsibilities separate when the application needs it.

For optional future study, [Microsoft RulesEngine](https://github.com/microsoft/RulesEngine) evaluates rules authored as JSON workflows or programmatically and returns evaluation results. [NRules](https://github.com/NRules/NRules) uses a C# rule DSL and Rete-based matching over session facts; rule actions can update facts and enable forward chaining. Neither library is a dependency or architecture decision for these exercises.

## Exercises

Exercises are self-contained and ordered by six-digit prefixes, with gaps for intermediate lessons. Each exercise has its own solution and README.

- **200000_RecordingWithSwitch**: model a recorder using enums and a switch before introducing the State pattern. Open `200000_RecordingWithSwitch\RecordingWithSwitch.slnx` to begin.
- **200100_RecordingWithSwitchAndTests**: repeat the switch-based recorder exercise with MSTest coverage for accepted transitions, rejected requests, and independent instances. Open `200100_RecordingWithSwitchAndTests\RecordingWithSwitchAndTests.slnx`.
- **201000_RecordingWithStatePattern**: implement the recorder behavior with state objects and compare it with the switch-based version. Open `201000_RecordingWithStatePattern\RecordingWithStatePattern.slnx`.
- **202000_RecordingWithEntryAndExit**: add state lifecycle hooks and verify exact execution order with an in-memory trace. Open `202000_RecordingWithEntryAndExit\RecordingWithEntryAndExit.slnx`.
- **203000_RecordingWithGuards**: guard Start with an injected permission check before lifecycle actions, without restricting Resume. Open `203000_RecordingWithGuards\RecordingWithGuards.slnx`.
- **204000_RecordingWithTransitionResults**: distinguish successful transitions, invalid requests, and guard denial with a typed result. Open `204000_RecordingWithTransitionResults\RecordingWithTransitionResults.slnx`.
- **205000_RecordingWithTransitionTable**: express the same state/request/target rules as centralized data while keeping the transition guard and lifecycle behavior. Open `205000_RecordingWithTransitionTable\RecordingWithTransitionTable.slnx`.
- **206000_RecordingWithHierarchy**: put Recording and Paused inside a RecordingSession composite and study parent lifecycle and permission checks. Open `206000_RecordingWithHierarchy\RecordingWithHierarchy.slnx`.
- **207000_RecordingWithBoundaryPermissions**: restrict parent permissions to entering and leaving RecordingSession; child changes keep the parent active without policy checks. Open `207000_RecordingWithBoundaryPermissions\RecordingWithBoundaryPermissions.slnx`.
- **208000_RecordingWithStatePermissions**: return to flat states and combine state-wide CanEnter/CanExit permissions with a Start-only transition guard. Open `208000_RecordingWithStatePermissions\RecordingWithStatePermissions.slnx`.
- **209000_RecordingWithBaseState**: extract shared permission defaults, lifecycle reporting, and debugger display into an abstract base state without changing the flat recorder behavior. Open `209000_RecordingWithBaseState\RecordingWithBaseState.slnx`.
- **210000_RecordingWithCachedStates**: lazily cache and reuse one state instance per mode per recorder, preserving recorder-specific policies and lifecycle behavior. Open `210000_RecordingWithCachedStates\RecordingWithCachedStates.slnx`.
- **211000_RecordingWithXmlTransitions**: load a validated flat transition table from XML while keeping typed state behavior and transition execution in C#. Open `211000_RecordingWithXmlTransitions\RecordingWithXmlTransitions.slnx`.
- **212000_RecordingWithSourceAwareTriggers**: add an XML trigger catalog and distinguish triggers by both name and external source, in addition to the current state. Open `212000_RecordingWithSourceAwareTriggers\RecordingWithSourceAwareTriggers.slnx`.
