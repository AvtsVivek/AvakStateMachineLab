namespace RecordingWithTransitionResults;

/// <summary>Accepts Resume and Stop; resuming does not require Start permission.</summary>
internal sealed class PausedState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Paused");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Paused");

    /// <inheritdoc />
    public TransitionResult TryHandle(Recorder recorder, RecordingRequest request)
    {
        switch (request)
        {
            case RecordingRequest.Resume:
                recorder.TransitionTo(new RecordingState());
                return TransitionResult.Transitioned;
            case RecordingRequest.Stop:
                recorder.TransitionTo(new IdleState());
                return TransitionResult.Transitioned;
            default:
                return TransitionResult.InvalidRequest;
        }
    }
}
