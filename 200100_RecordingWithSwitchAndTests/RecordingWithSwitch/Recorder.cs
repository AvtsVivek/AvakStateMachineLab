namespace RecordingWithSwitch;

public sealed class Recorder
{
    public RecordingState CurrentState { get; private set; } = RecordingState.Idle;

    public bool TryHandle(RecordingRequest request)
    {
        switch (CurrentState)
        {
            case RecordingState.Idle when request == RecordingRequest.Start:
                CurrentState = RecordingState.Recording;
                return true;

            case RecordingState.Recording when request == RecordingRequest.Pause:
                CurrentState = RecordingState.Paused;
                return true;

            case RecordingState.Recording when request == RecordingRequest.Stop:
                CurrentState = RecordingState.Idle;
                return true;

            case RecordingState.Paused when request == RecordingRequest.Resume:
                CurrentState = RecordingState.Recording;
                return true;

            case RecordingState.Paused when request == RecordingRequest.Stop:
                CurrentState = RecordingState.Idle;
                return true;

            default:
                return false;
        }
    }
}
