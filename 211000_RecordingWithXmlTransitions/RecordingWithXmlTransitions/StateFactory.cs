namespace RecordingWithXmlTransitions;

internal sealed class StateFactory(Func<bool> canLeaveRecording, Func<bool> canEnterPaused)
{
    private IdleState? _idle;
    private RecordingState? _recording;
    private PausedState? _paused;

    public IRecorderState Get(RecordingMode mode) => mode switch
    {
        RecordingMode.Idle => _idle ??= new IdleState(),
        RecordingMode.Recording => _recording ??= new RecordingState(canLeaveRecording),
        RecordingMode.Paused => _paused ??= new PausedState(canEnterPaused),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown recording mode.")
    };
}
