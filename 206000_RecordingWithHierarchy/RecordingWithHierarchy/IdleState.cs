namespace RecordingWithHierarchy;

/// <summary>Provides lifecycle hooks for the top-level Idle state.</summary>
internal sealed class IdleState
{
    /// <summary>Runs when Idle becomes active.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    public void Enter(Action<string> reportActivity) => reportActivity("Enter Idle");

    /// <summary>Runs immediately before leaving Idle.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    public void Exit(Action<string> reportActivity) => reportActivity("Exit Idle");
}
