namespace RecordingWithTransitionResults;

/// <summary>Coordinates state objects, a Start permission guard, lifecycle actions, and request results.</summary>
/// <remarks>
/// This synchronous example is not thread-safe. Guard exceptions leave the source active.
/// Lifecycle exceptions propagate without rollback: exit failure leaves the source active,
/// while entry failure leaves the target active.
/// </remarks>
public sealed class Recorder
{
    private readonly Func<bool> _canStartRecording;
    private readonly Action<string> _reportActivity;
    private IRecorderState _currentState = new IdleState();
    private bool _isHandlingRequest;

    /// <summary>Creates a recorder and enters Idle once without evaluating the Start guard.</summary>
    /// <param name="canStartRecording">A side-effect-free permission check used only for Start in Idle.</param>
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

    /// <summary>Handles a request and identifies whether it transitioned, was invalid, or was guard-denied.</summary>
    /// <param name="request">The request acting as the transition trigger.</param>
    /// <returns>
    /// <see cref="TransitionResult.Transitioned"/> when lifecycle work completes;
    /// <see cref="TransitionResult.InvalidRequest"/> when the active state has no matching transition;
    /// <see cref="TransitionResult.GuardDenied"/> when Start permission is refused.
    /// </returns>
    /// <exception cref="InvalidOperationException">A guard or lifecycle callback sends a nested request.</exception>
    /// <remarks>Guard and lifecycle exceptions propagate; they are not converted to result values.</remarks>
    public TransitionResult TryHandle(RecordingRequest request)
    {
        if (_isHandlingRequest)
        {
            throw new InvalidOperationException("A guard or lifecycle callback cannot send a nested request.");
        }

        _isHandlingRequest = true;
        try
        {
            return _currentState.TryHandle(this, request);
        }
        finally
        {
            _isHandlingRequest = false;
        }
    }

    /// <summary>Evaluates the injected Start permission check without caching its result.</summary>
    /// <returns>Whether the Idle-to-Recording transition triggered by Start is permitted.</returns>
    internal bool CanStartRecording() => _canStartRecording();

    /// <summary>Exits the source, replaces the active state, and then enters the target.</summary>
    /// <param name="state">The target selected after the request and guard checks succeed.</param>
    internal void TransitionTo(IRecorderState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _currentState.Exit(_reportActivity);
        _currentState = state;
        _currentState.Enter(_reportActivity);
    }
}
