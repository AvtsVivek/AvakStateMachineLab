namespace RecordingWithHierarchy;

/// <summary>Provides lifecycle hooks for the top-level Idle state.</summary>
internal sealed class IdleState
{
    /// <summary>Gets the mode represented by this top-level state.</summary>
    public RecordingMode Mode => RecordingMode.Idle;

    /// <summary>Runs when Idle becomes active.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <summary>Runs immediately before leaving Idle.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
