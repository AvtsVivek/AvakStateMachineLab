namespace RecordingWithTransitionResults;

/// <summary>Accepts Start only when the transition-specific permission check succeeds.</summary>
internal sealed class IdleState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Idle;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");

    /// <inheritdoc />
    public TransitionResult TryHandle(Recorder recorder, RecordingRequest request)
    {
        if (request != RecordingRequest.Start)
        {
            return TransitionResult.InvalidRequest;
        }

        if (!recorder.CanStartRecording())
        {
            return TransitionResult.GuardDenied;
        }

        recorder.TransitionTo(new RecordingState());
        return TransitionResult.Transitioned;
    }
}
