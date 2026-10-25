namespace RecordingWithEntryAndExit;

/// <summary>
/// Allows an active recording to pause or stop and reports recording entry and exit activity.
/// </summary>
internal sealed class RecordingState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Recording;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Recording");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Recording");

    /// <inheritdoc />
    public bool TryHandle(Recorder recorder, RecordingRequest request)
    {
        switch (request)
        {
            case RecordingRequest.Pause:
                recorder.TransitionTo(new PausedState());
                return true;

            case RecordingRequest.Stop:
                recorder.TransitionTo(new IdleState());
                return true;

            default:
                return false;
        }
    }
}
