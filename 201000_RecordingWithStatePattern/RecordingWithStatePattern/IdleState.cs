namespace RecordingWithStatePattern;

/// <summary>
/// Handles requests while the recorder is idle.
/// </summary>
internal sealed class IdleState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Idle;

    /// <inheritdoc />
    public bool TryHandle(Recorder recorder, RecordingRequest request)
    {
        if (request != RecordingRequest.Start)
        {
            return false;
        }

        recorder.TransitionTo(new RecordingState());
        return true;
    }
}
