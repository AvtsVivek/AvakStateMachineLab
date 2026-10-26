namespace RecordingWithTransitionTable;

/// <summary>Provides lifecycle hooks for the Paused mode; transition decisions live in the table.</summary>
internal sealed class PausedState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Paused");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Paused");
}
