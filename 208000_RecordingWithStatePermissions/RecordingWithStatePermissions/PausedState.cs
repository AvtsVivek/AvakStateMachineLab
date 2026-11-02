namespace RecordingWithStatePermissions;

/// <summary>Requires permission to enter Paused, while allowing unrestricted departure.</summary>
[System.Diagnostics.DebuggerDisplay("{Mode.ToString(),nq}")]
internal sealed class PausedState : IRecorderState
{
    private readonly Func<bool> _canEnterPaused;

    /// <summary>Creates a state with a policy such as device support for pausing.</summary>
    /// <param name="canEnterPaused">Checks whether entering Paused is allowed.</param>
    public PausedState(Func<bool> canEnterPaused)
    {
        _canEnterPaused = canEnterPaused;
    }

    /// <inheritdoc />
    public RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public bool CanEnter() => _canEnterPaused();

    /// <inheritdoc />
    public bool CanExit() => true;

    /// <inheritdoc />
    public void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <inheritdoc />
    public void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
