# 212000 index

- [README](README.md): design, XML formats and execution.
- [Trigger catalog](RecordingWithSourceAwareTriggers/recorder-triggers.xml): allowed name/source pairs.
- [Transition table](RecordingWithSourceAwareTriggers/recorder-transitions.xml): configured transitions.
- [TriggerCatalog](RecordingWithSourceAwareTriggers/TriggerCatalog.cs): catalog loading and validation.
- [TransitionTable](RecordingWithSourceAwareTriggers/TransitionTable.cs): transition loading and lookup.
- [Recorder](RecordingWithSourceAwareTriggers/Recorder.cs): guarded dispatch and lifecycle.
- [Trigger source tests](RecordingWithSourceAwareTriggers.Tests/TriggerSourceTests.cs): identity and validation tests.
