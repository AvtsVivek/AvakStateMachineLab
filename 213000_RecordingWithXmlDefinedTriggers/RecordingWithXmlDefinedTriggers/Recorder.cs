namespace RecordingWithXmlDefinedTriggers;

/// <summary>Dispatches XML-defined triggers through guarded state transitions.</summary>
[System.Diagnostics.DebuggerDisplay("CurrentState = {CurrentState.ToString(),nq}")]
public sealed class Recorder
{
    private readonly Func<bool> _canStartRecording;
    private readonly Action<string> _reportActivity;
    private readonly TransitionTable _transitionTable;
    private readonly StateFactory _stateFactory;
    private IRecorderState _currentState;
    private bool _isHandlingRequest;

    public Recorder(TransitionTable transitionTable, Func<bool> canStartRecording,
        Func<bool> canLeaveRecording, Func<bool> canEnterPaused, Action<string> reportActivity)
    {
        ArgumentNullException.ThrowIfNull(transitionTable);
        ArgumentNullException.ThrowIfNull(canStartRecording);
        ArgumentNullException.ThrowIfNull(canLeaveRecording);
        ArgumentNullException.ThrowIfNull(canEnterPaused);
        ArgumentNullException.ThrowIfNull(reportActivity);

        _transitionTable = transitionTable;
        _canStartRecording = canStartRecording;
        _reportActivity = reportActivity;
        _stateFactory = new StateFactory(canLeaveRecording, canEnterPaused);
        _currentState = _stateFactory.Get(transitionTable.InitialMode);
        _currentState.Enter(_reportActivity);
    }

    public RecordingMode CurrentState => _currentState.Mode;

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

            if (transition.RequiresStartPermission && !_canStartRecording())
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
