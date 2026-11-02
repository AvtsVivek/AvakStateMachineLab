namespace RecordingWithBaseState;

/// <summary>Overrides only exit permission; other behavior comes from RecorderStateBase.</summary>
internal sealed class RecordingState : RecorderStateBase
{
    private readonly Func<bool> _canLeaveRecording;

    /// <summary>Creates a state with an exit policy shared by Pause and Stop.</summary>
    /// <param name="canLeaveRecording">Checks whether leaving Recording is allowed.</param>
    public RecordingState(Func<bool> canLeaveRecording)
    {
        _canLeaveRecording = canLeaveRecording;
    }

    /// <inheritdoc />
    public override RecordingMode Mode => RecordingMode.Recording;

    /// <inheritdoc />
    public override bool CanExit() => _canLeaveRecording();
}
