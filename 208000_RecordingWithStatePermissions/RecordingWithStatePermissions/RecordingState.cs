namespace RecordingWithStatePermissions;

/// <summary>Applies the same exit restriction to both Pause and Stop, regardless of destination.</summary>
internal sealed class RecordingState : IRecorderState
{
    private readonly Func<bool> _canLeaveRecording;

    /// <summary>Creates a state with a policy such as no write being pending.</summary>
    /// <param name="canLeaveRecording">Checks whether any departure from Recording is allowed.</param>
    public RecordingState(Func<bool> canLeaveRecording)
    {
        _canLeaveRecording = canLeaveRecording;
    }

    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Recording;

    /// <inheritdoc />
    public bool CanEnter() => true;

    /// <inheritdoc />
    public bool CanExit() => _canLeaveRecording();

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
