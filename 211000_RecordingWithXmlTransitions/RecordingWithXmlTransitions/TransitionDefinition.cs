namespace RecordingWithXmlTransitions;

internal sealed record TransitionDefinition(RecordingMode Target, bool RequiresStartPermission);
