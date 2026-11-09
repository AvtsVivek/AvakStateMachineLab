namespace RecordingWithXmlDefinedGuards;

/// <summary>Dispatches XML-defined triggers through guarded state transitions using an injected guard registry.</summary>
[System.Diagnostics.DebuggerDisplay("CurrentState = {CurrentState.ToString(),nq}")]
public sealed class Recorder
{
    private readonly GuardRegistry _guardRegistry;
    private readonly Action<string> _reportActivity;
    private readonly TransitionTable _transitionTable;
    private readonly StateFactory _stateFactory;
    private IRecorderState _currentState;
    private bool _isHandlingRequest;

    /// <summary>Creates a recorder that uses XML-defined triggers, guards, and transitions.</summary>
    /// <param name="transitionTable">The loaded XML configuration defining triggers, states, and transitions.</param>
    /// <param name="guardRegistry">The guard registry mapping XML guard names to C# implementations.</param>
    /// <param name="canLeaveRecording">State-wide permission check for Recording exit; must not send nested requests.</param>
    /// <param name="canEnterPaused">State-wide permission check for Paused entry; must not send nested requests.</param>
    /// <param name="reportActivity">Receives lifecycle activity synchronously; it must not send nested requests.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public Recorder(TransitionTable transitionTable, GuardRegistry guardRegistry,
        Func<bool> canLeaveRecording, Func<bool> canEnterPaused, Action<string> reportActivity)
    {
        ArgumentNullException.ThrowIfNull(transitionTable);
        ArgumentNullException.ThrowIfNull(guardRegistry);
        ArgumentNullException.ThrowIfNull(canLeaveRecording);
        ArgumentNullException.ThrowIfNull(canEnterPaused);
        ArgumentNullException.ThrowIfNull(reportActivity);

        _transitionTable = transitionTable;
        _guardRegistry = guardRegistry;
        _reportActivity = reportActivity;
        _stateFactory = new StateFactory(canLeaveRecording, canEnterPaused);
        _currentState = _stateFactory.Get(transitionTable.InitialMode);
        _currentState.Enter(_reportActivity);
    }

    public RecordingMode CurrentState => _currentState.Mode;

    /// <summary>Attempts to handle a trigger, evaluating XML-defined transitions and guard conditions.</summary>
    /// <param name="trigger">The trigger to process.</param>
    /// <returns>
    /// Transitioned if the state/trigger pair is valid and all guards pass.
    /// InvalidRequest if the pair is unlisted or has no transition from the current state.
    /// GuardDenied if a transition exists but a registered guard or state permission returns false.
    /// </returns>
    /// <exception cref="ArgumentException">Trigger name or source is null or empty, or a guard used by this transition is not registered.</exception>
    /// <exception cref="InvalidOperationException">A permission or lifecycle callback sends a nested request.</exception>
    public TransitionResult TryHandle(RecordingTrigger trigger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger.Source);
        if (_isHandlingRequest)
        {
            throw new InvalidOperationException("A permission or lifecycle callback cannot send a nested request.");
        }

        _isHandlingRequest = true;
        try
        {
            if (!_transitionTable.TryGet(CurrentState, trigger, out TransitionDefinition? transition)
                || transition is null)
            {
                return TransitionResult.InvalidRequest;
            }

            if (transition.GuardName is not null && !_guardRegistry.Evaluate(transition.GuardName))
            {
                return TransitionResult.GuardDenied;
            }

            IRecorderState targetState = _stateFactory.Get(transition.Target);
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
