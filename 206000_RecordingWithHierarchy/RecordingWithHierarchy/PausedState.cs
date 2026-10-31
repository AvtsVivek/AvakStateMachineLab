namespace RecordingWithHierarchy;

/// <summary>Provides lifecycle hooks for the Paused child of the active session.</summary>
internal sealed class PausedState : IRecordingSubstate
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Paused");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Paused");
}
