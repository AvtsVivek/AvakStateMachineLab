namespace RecordingWithXmlDefinedGuards;

internal sealed record TransitionDefinition(RecordingMode Target, string? GuardName);
