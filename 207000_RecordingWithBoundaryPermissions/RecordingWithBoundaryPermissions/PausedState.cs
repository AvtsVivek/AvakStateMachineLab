namespace RecordingWithBoundaryPermissions;

/// <summary>Provides lifecycle behavior for the session's Paused child.</summary>
[System.Diagnostics.DebuggerDisplay("{Mode.ToString(),nq}")]
internal sealed class PausedState : IRecordingSubstate
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
