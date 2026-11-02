namespace RecordingWithBaseState;

/// <summary>Constructs states and supplies their state-wide permission dependencies.</summary>
internal static class StateFactory
{
    /// <summary>Creates the state selected by a transition row without running checks or hooks.</summary>
    /// <param name="mode">The target mode.</param>
    /// <param name="canLeaveRecording">The Recording exit policy.</param>
    /// <param name="canEnterPaused">The Paused entry policy.</param>
    /// <returns>The state object for the requested mode.</returns>
    internal static IRecorderState Create(
        RecordingMode mode, Func<bool> canLeaveRecording, Func<bool> canEnterPaused) => mode switch
    {
        RecordingMode.Idle => new IdleState(),
        RecordingMode.Recording => new RecordingState(canLeaveRecording),
        RecordingMode.Paused => new PausedState(canEnterPaused),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown recording mode.")
    };
}
