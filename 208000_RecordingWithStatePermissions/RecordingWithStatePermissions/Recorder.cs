namespace RecordingWithStatePermissions;

/// <summary>Combines transition-specific guards with flat state-wide entry and exit permissions.</summary>
/// <remarks>
/// Checks are synchronous and stop at the first denial. Exceptions propagate without rollback:
/// exit failure retains the source, while entry failure retains the selected target.
/// This teaching example is single-threaded and does not reserve resources during permission checks.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("CurrentState = {CurrentState.ToString(),nq}")]
public sealed class Recorder
{
    private readonly Func<bool> _canStartRecording;
    private readonly Func<bool> _canLeaveRecording;
    private readonly Func<bool> _canEnterPaused;
    private readonly Action<string> _reportActivity;
    private IRecorderState _currentState = new IdleState();
    private bool _isHandlingRequest;

    /// <summary>Validates callbacks and enters Idle without checking transition permissions.</summary>
    /// <param name="canStartRecording">A guard only for Idle + Start, not for every entry to Recording.</param>
    /// <param name="canLeaveRecording">Recording.CanExit policy, used for both Pause and Stop.</param>
    /// <param name="canEnterPaused">Paused.CanEnter policy, used whenever Paused is a transition target.</param>
    /// <param name="reportActivity">Receives entry and exit activity synchronously.</param>
    /// <exception cref="ArgumentNullException">A required callback is null.</exception>
    public Recorder(Func<bool> canStartRecording, Func<bool> canLeaveRecording,
        Func<bool> canEnterPaused, Action<string> reportActivity)
    {
        ArgumentNullException.ThrowIfNull(canStartRecording);
        ArgumentNullException.ThrowIfNull(canLeaveRecording);
        ArgumentNullException.ThrowIfNull(canEnterPaused);
        ArgumentNullException.ThrowIfNull(reportActivity);
        _canStartRecording = canStartRecording;
        _canLeaveRecording = canLeaveRecording;
        _canEnterPaused = canEnterPaused;
        _reportActivity = reportActivity;
        _currentState.Enter(_reportActivity);
    }

    /// <summary>Gets the mode derived from the active state object.</summary>
    public RecordingMode CurrentState => _currentState.Mode;

    /// <summary>Checks transition existence, its guard, source CanExit, and target CanEnter before hooks.</summary>
    /// <param name="request">The trigger to look up with the current mode.</param>
    /// <returns>Transitioned, InvalidRequest, or GuardDenied for any denied permission.</returns>
    /// <exception cref="InvalidOperationException">A permission or lifecycle callback sends a nested request.</exception>
    /// <remarks>Permissions should be side-effect-free. Exceptions are not converted to result values.</remarks>
    public TransitionResult TryHandle(RecordingRequest request)
    {
        if (_isHandlingRequest)
        {
            throw new InvalidOperationException("A permission or lifecycle callback cannot send a nested request.");
        }

        _isHandlingRequest = true;
        try
        {
            if (!TransitionTable.TryGet(CurrentState, request, out TransitionDefinition? transition)
                || transition is null)
            {
                return TransitionResult.InvalidRequest;
            }

            if (transition.RequiresStartPermission && !_canStartRecording())
            {
                return TransitionResult.GuardDenied;
            }

            IRecorderState targetState = StateFactory.Create(transition.Target, _canLeaveRecording, _canEnterPaused);
            if (!_currentState.CanExit() || !targetState.CanEnter())
            {
                return TransitionResult.GuardDenied;
            }

            _currentState.Exit(_reportActivity);
            _currentState = targetState;
            _currentState.Enter(_reportActivity);
            return TransitionResult.Transitioned;
        }
        finally
        {
            _isHandlingRequest = false;
        }
    }
}
