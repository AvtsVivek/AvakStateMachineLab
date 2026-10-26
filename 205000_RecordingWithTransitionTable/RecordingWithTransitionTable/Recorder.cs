namespace RecordingWithTransitionTable;

/// <summary>Uses a centralized transition table and coordinates guards and lifecycle actions.</summary>
/// <remarks>
/// This synchronous example is not thread-safe. Guard exceptions leave the source active.
/// Lifecycle exceptions propagate without rollback: exit failure leaves the source active,
/// while entry failure leaves the target active.
/// </remarks>
public sealed class Recorder
{
    private readonly Func<bool> _canStartRecording;
    private readonly Action<string> _reportActivity;
    private IRecorderState _currentState = StateFactory.Create(RecordingMode.Idle);
    private bool _isHandlingRequest;

    /// <summary>Creates a recorder and enters Idle once without evaluating the Start guard.</summary>
    /// <param name="canStartRecording">A side-effect-free permission check used only by the guarded table row.</param>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    /// <exception cref="ArgumentNullException">Either callback is null.</exception>
    public Recorder(Func<bool> canStartRecording, Action<string> reportActivity)
    {
        ArgumentNullException.ThrowIfNull(canStartRecording);
        ArgumentNullException.ThrowIfNull(reportActivity);
        _canStartRecording = canStartRecording;
        _reportActivity = reportActivity;
        _currentState.Enter(_reportActivity);
    }

    /// <summary>Gets the observable mode derived from the active state object.</summary>
    public RecordingMode CurrentState => _currentState.Mode;

    /// <summary>Looks up a transition row, evaluates its guard if required, and performs its lifecycle.</summary>
    /// <param name="request">The request to look up with the current mode in the transition table.</param>
    /// <returns>A result that distinguishes a completed transition, invalid pair, or denied guard.</returns>
    /// <exception cref="InvalidOperationException">A guard or lifecycle callback sends a nested request.</exception>
    /// <remarks>Guard and lifecycle exceptions propagate and are not converted to result values.</remarks>
    public TransitionResult TryHandle(RecordingRequest request)
    {
        if (_isHandlingRequest)
        {
            throw new InvalidOperationException("A guard or lifecycle callback cannot send a nested request.");
        }

        _isHandlingRequest = true;
        try
        {
            if (!TransitionTable.TryGet(_currentState.Mode, request, out TransitionDefinition? transition)
                || transition is null)
            {
                return TransitionResult.InvalidRequest;
            }

            if (transition.RequiresStartPermission && !_canStartRecording())
            {
                return TransitionResult.GuardDenied;
            }

            IRecorderState targetState = StateFactory.Create(transition.Target);
            TransitionTo(targetState);
            return TransitionResult.Transitioned;
        }
        finally
        {
            _isHandlingRequest = false;
        }
    }

    /// <summary>Exits the source, replaces the active state, and then enters the target.</summary>
    /// <param name="state">The state object selected by the matching transition row.</param>
    private void TransitionTo(IRecorderState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _currentState.Exit(_reportActivity);
        _currentState = state;
        _currentState.Enter(_reportActivity);
    }
}
