namespace RecordingWithStatePermissions;

/// <summary>Describes a destination and whether this particular transition has the Start guard.</summary>
/// <param name="Target">The mode to enter if every applicable check passes.</param>
/// <param name="RequiresStartPermission">True only for Idle + Start, never Paused + Resume.</param>
internal sealed record TransitionDefinition(RecordingMode Target, bool RequiresStartPermission);
