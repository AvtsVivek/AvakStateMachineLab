namespace RecordingWithSourceAwareTriggers;

/// <summary>Identifies a request and the external component that emitted it.</summary>
public readonly record struct RecordingTrigger(RecordingRequest Name, string Source);
