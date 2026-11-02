namespace RecordingWithCachedStates;

/// <summary>Overrides only entry permission; other behavior comes from RecorderStateBase.</summary>
internal sealed class PausedState : RecorderStateBase
{
    private readonly Func<bool> _canEnterPaused;

    /// <summary>Creates a state with a permission checked whenever Paused is a transition target.</summary>
    /// <param name="canEnterPaused">Checks whether entering Paused is allowed.</param>
    public PausedState(Func<bool> canEnterPaused)
    {
        _canEnterPaused = canEnterPaused;
    }

    /// <inheritdoc />
    public override RecordingMode Mode => RecordingMode.Paused;

    /// <inheritdoc />
    public override bool CanEnter() => _canEnterPaused();
}
