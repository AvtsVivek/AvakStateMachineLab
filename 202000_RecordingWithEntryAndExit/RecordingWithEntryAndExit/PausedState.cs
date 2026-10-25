namespace RecordingWithEntryAndExit;

/// <summary>
/// Allows a paused recording to resume or stop and reports paused entry and exit activity.
/// </summary>
internal sealed class PausedState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Paused");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Paused");

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
