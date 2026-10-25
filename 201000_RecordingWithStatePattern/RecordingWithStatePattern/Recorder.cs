namespace RecordingWithStatePattern;

/// <summary>
/// Context whose request handling is delegated to its current state object.
/// </summary>
public sealed class Recorder
{
    private IRecorderState _currentState = new IdleState();

    /// <summary>
    /// Gets the current externally observable recording mode.
    /// </summary>
    public RecordingMode CurrentState => _currentState.Mode;

    /// <summary>
    /// Delegates a request to the current state object.
    /// </summary>
    /// <param name="request">The request acting as the trigger to handle.</param>
    /// <returns><see langword="true"/> if the current state accepted the request; otherwise, <see langword="false"/>.</returns>
    public bool TryHandle(RecordingRequest request)
    {
        return _currentState.TryHandle(this, request);
    }

    /// <summary>
    /// Changes the active state object. State implementations use this method after accepting a request.
    /// </summary>
    /// <param name="state">The next state object for this recorder.</param>
    internal void TransitionTo(IRecorderState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _currentState = state;
    }
}
