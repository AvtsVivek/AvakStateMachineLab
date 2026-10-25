namespace RecordingWithEntryAndExit;

/// <summary>
/// Delegates requests to a state object and coordinates exit, state replacement, and entry.
/// </summary>
/// <remarks>
/// This synchronous learning example is not thread-safe. Lifecycle exceptions propagate without
/// rollback: exit failure leaves the source active, while entry failure leaves the target active.
/// </remarks>
public sealed class Recorder
{
    private readonly Action<string> _reportActivity;
    private IRecorderState _currentState = new IdleState();
    private bool _isHandlingRequest;

    /// <summary>
    /// Creates a recorder and immediately runs the initial idle state's entry action once.
    /// </summary>
    /// <param name="reportActivity">
    /// A synchronous callback invoked by entry and exit actions. Use a list's Add method in tests
    /// or Console.WriteLine in the demonstration. The callback must not send nested requests.
    /// </param>
    /// <exception cref="ArgumentNullException">The callback is null.</exception>
    public Recorder(Action<string> reportActivity)
    {
        ArgumentNullException.ThrowIfNull(reportActivity);
        _reportActivity = reportActivity;
        _currentState.Enter(_reportActivity);
    }

    /// <summary>Gets the public mode derived from the active state object.</summary>
    public RecordingMode CurrentState => _currentState.Mode;

    /// <summary>
    /// Handles a request. Acceptance runs the source exit before switching state and running target entry.
    /// Rejection runs neither lifecycle action and leaves the state unchanged.
    /// </summary>
    /// <param name="request">The request acting as the trigger.</param>
    /// <returns>True for an accepted request whose lifecycle actions completed; false for rejection.</returns>
    /// <exception cref="InvalidOperationException">A callback attempts to handle a nested request.</exception>
    public bool TryHandle(RecordingRequest request)
    {
        if (_isHandlingRequest)
        {
            throw new InvalidOperationException("A lifecycle callback cannot send a nested request.");
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

    /// <summary>
    /// Runs exit while the source is active, replaces the state, then runs entry while the target is active.
    /// </summary>
    /// <param name="state">The state selected by an accepted request.</param>
    internal void TransitionTo(IRecorderState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _currentState.Exit(_reportActivity);
        _currentState = state;
        _currentState.Enter(_reportActivity);
    }
}
