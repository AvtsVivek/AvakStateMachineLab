namespace RecordingWithStatePattern;

/// <summary>
/// Handles requests while the recorder is paused.
/// </summary>
internal sealed class PausedState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public bool TryHandle(Recorder recorder, RecordingRequest request)
    {
        switch (request)
        {
            case RecordingRequest.Resume:
                recorder.TransitionTo(new RecordingState());
                return true;

            case RecordingRequest.Stop:
                recorder.TransitionTo(new IdleState());
                return true;

            default:
                return false;
        }
    }
}
