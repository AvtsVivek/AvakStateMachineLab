namespace RecordingWithXmlDefinedTriggers;

internal sealed record TransitionDefinition(RecordingMode Target, bool RequiresStartPermission);
