namespace RecordingWithBoundaryPermissions;

/// <summary>Provides lifecycle behavior outside the recording session.</summary>
[System.Diagnostics.DebuggerDisplay("{Mode.ToString(),nq}")]
internal sealed class IdleState
{
    /// <summary>Gets the mode represented by this top-level state.</summary>
    public RecordingMode Mode => RecordingMode.Idle;

    /// <summary>Reports entry after Idle becomes active.</summary>
    /// <param name="reportActivity">Receives lifecycle activity.</param>
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <summary>Reports exit before leaving Idle.</summary>
    /// <param name="reportActivity">Receives lifecycle activity.</param>
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
