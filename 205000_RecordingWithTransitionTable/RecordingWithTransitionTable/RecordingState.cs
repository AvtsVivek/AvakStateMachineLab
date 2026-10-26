namespace RecordingWithTransitionTable;

/// <summary>Provides lifecycle hooks for the Recording mode; transition decisions live in the table.</summary>
internal sealed class RecordingState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Recording;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Recording");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Recording");
}
