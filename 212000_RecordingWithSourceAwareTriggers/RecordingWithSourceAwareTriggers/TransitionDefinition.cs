namespace RecordingWithSourceAwareTriggers;

internal sealed record TransitionDefinition(RecordingMode Target, bool RequiresStartPermission);
