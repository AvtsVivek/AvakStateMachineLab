namespace RecordingWithCachedStates;

/// <summary>Lazily creates and reuses one state instance per mode for one recorder.</summary>
internal sealed class StateFactory
{
    private readonly Func<bool> _canLeaveRecording;
    private readonly Func<bool> _canEnterPaused;
    private IdleState? _idle;
    private RecordingState? _recording;
    private PausedState? _paused;

    /// <summary>Creates a per-recorder cache without constructing states or evaluating permissions.</summary>
    /// <param name="canLeaveRecording">The policy captured by this recorder's Recording state.</param>
    /// <param name="canEnterPaused">The policy captured by this recorder's Paused state.</param>
    public StateFactory(Func<bool> canLeaveRecording, Func<bool> canEnterPaused)
    {
        ArgumentNullException.ThrowIfNull(canLeaveRecording);
        ArgumentNullException.ThrowIfNull(canEnterPaused);
        _canLeaveRecording = canLeaveRecording;
        _canEnterPaused = canEnterPaused;
    }

    /// <summary>Returns the cached state for a mode, creating that mode's instance on first use.</summary>
    /// <param name="mode">The state to retrieve.</param>
    /// <returns>The same instance for each request for this mode from this factory.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The mode has no state implementation.</exception>
    public IRecorderState Get(RecordingMode mode) => mode switch
    {
        RecordingMode.Idle => _idle ??= new IdleState(),
        RecordingMode.Recording => _recording ??= new RecordingState(_canLeaveRecording),
        RecordingMode.Paused => _paused ??= new PausedState(_canEnterPaused),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown recording mode.")
    };
}
