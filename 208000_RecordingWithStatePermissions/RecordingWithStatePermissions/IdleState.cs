namespace RecordingWithStatePermissions;

/// <summary>Allows unrestricted entry and exit; Start's guard belongs to its transition.</summary>
internal sealed class IdleState : IRecorderState
{
    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Idle;

    /// <inheritdoc />
    public bool CanEnter() => true;

    /// <inheritdoc />
    public bool CanExit() => true;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
