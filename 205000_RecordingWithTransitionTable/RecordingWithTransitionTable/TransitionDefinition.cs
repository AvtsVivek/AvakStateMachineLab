namespace RecordingWithTransitionTable;

/// <summary>Describes one transition as source mode, request, target mode, and guard requirement.</summary>
/// <param name="Source">The mode from which this transition is available.</param>
/// <param name="Request">The request that triggers the transition.</param>
/// <param name="Target">The mode reached when the transition completes.</param>
/// <param name="RequiresStartPermission">Whether the Start permission guard must pass before lifecycle work.</param>
internal sealed record TransitionDefinition(
    RecordingMode Source,
    RecordingRequest Request,
    RecordingMode Target,
    bool RequiresStartPermission);
