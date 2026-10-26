namespace RecordingWithTransitionTable;

/// <summary>Creates the lifecycle state object corresponding to an observable mode.</summary>
internal static class StateFactory
{
    /// <summary>Creates the state object representing the requested mode.</summary>
    /// <param name="mode">The mode for which to create a state object.</param>
    /// <returns>A new state object for the mode.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The mode does not have a state implementation.</exception>
    internal static IRecorderState Create(RecordingMode mode) => mode switch
    {
        RecordingMode.Idle => new IdleState(),
        RecordingMode.Recording => new RecordingState(),
        RecordingMode.Paused => new PausedState(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown recording mode.")
    };
}
