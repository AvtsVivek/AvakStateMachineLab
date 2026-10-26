namespace RecordingWithTransitionResults;

/// <summary>Accepts Pause and Stop without evaluating the Start guard.</summary>
internal sealed class RecordingState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Recording;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Recording");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Recording");

    /// <inheritdoc />
    public TransitionResult TryHandle(Recorder recorder, RecordingRequest request)
    {
        switch (request)
        {
            case RecordingRequest.Pause:
                recorder.TransitionTo(new PausedState());
                return TransitionResult.Transitioned;
            case RecordingRequest.Stop:
                recorder.TransitionTo(new IdleState());
                return TransitionResult.Transitioned;
            default:
                return TransitionResult.InvalidRequest;
        }
    }
}
