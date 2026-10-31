namespace RecordingWithHierarchy;

/// <summary>Provides lifecycle hooks for the Recording child of the active session.</summary>
internal sealed class RecordingState : IRecordingSubstate
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Recording;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
