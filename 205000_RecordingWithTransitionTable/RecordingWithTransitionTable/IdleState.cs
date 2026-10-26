namespace RecordingWithTransitionTable;

/// <summary>Provides lifecycle hooks for the Idle mode; transition decisions live in the table.</summary>
internal sealed class IdleState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Idle;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Idle");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Idle");
}
