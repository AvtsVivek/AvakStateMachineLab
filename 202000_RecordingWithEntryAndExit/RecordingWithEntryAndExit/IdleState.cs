namespace RecordingWithEntryAndExit;

/// <summary>
/// Allows recording to start from idle and reports idle entry and exit activity.
/// </summary>
internal sealed class IdleState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Idle;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Idle");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Idle");

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
