# AvakStateMachineLab
A hands-on exploration of the State pattern and state machines in .NET.

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
