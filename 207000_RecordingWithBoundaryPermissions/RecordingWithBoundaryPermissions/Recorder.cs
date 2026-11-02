namespace RecordingWithBoundaryPermissions;

/// <summary>Coordinates an exclusive recording hierarchy with parent permissions checked only at its boundary.</summary>
/// <remarks>
/// This example is synchronous and not thread-safe. Callback exceptions propagate without rollback;
/// a source exit failure retains the source, and an entry failure retains the selected target.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("CurrentState = {CurrentState.ToString(),nq}")]
public sealed class Recorder
{
    private readonly Func<bool> _canStartRecording;
    private readonly Func<bool> _canEnterSession;
    private readonly Func<bool> _canExitSession;
    private readonly Action<string> _reportActivity;
    private readonly IdleState _idleState = new();
    private RecordingSessionState? _session;
    private bool _isHandlingRequest;

    /// <summary>Creates and enters Idle without allocating a session or evaluating permission.</summary>
    /// <param name="canStartRecording">Checks Start-specific permission before parent-entry permission.</param>
    /// <param name="canEnterSession">Checks only Start, which crosses into RecordingSession.</param>
    /// <param name="canExitSession">Checks only Stop, which crosses out of RecordingSession.</param>
    /// <param name="reportActivity">Receives lifecycle messages synchronously; callbacks must not issue nested requests.</param>
    /// <exception cref="ArgumentNullException">Any callback is null.</exception>
    public Recorder(Func<bool> canStartRecording, Func<bool> canEnterSession,
        Func<bool> canExitSession, Action<string> reportActivity)
    {
        ArgumentNullException.ThrowIfNull(canStartRecording);
        ArgumentNullException.ThrowIfNull(canEnterSession);
        ArgumentNullException.ThrowIfNull(canExitSession);
        ArgumentNullException.ThrowIfNull(reportActivity);
        _canStartRecording = canStartRecording;
        _canEnterSession = canEnterSession;
        _canExitSession = canExitSession;
        _reportActivity = reportActivity;
        _idleState.Enter(_reportActivity);
    }

    /// <summary>Gets the active leaf mode without triggering session allocation.</summary>
    public RecordingMode CurrentState => _session?.ActiveMode ?? _idleState.Mode;

    /// <summary>Handles a request, checking parent permission only when crossing the session boundary.</summary>
    /// <param name="request">The requested action.</param>
    /// <returns>Transitioned after lifecycle completion, InvalidRequest for undefined pairs, or GuardDenied.</returns>
    /// <exception cref="InvalidOperationException">A callback attempts a nested request.</exception>
    /// <remarks>Exceptions are not converted into rejection results; the processing flag resets on every outcome.</remarks>
    public TransitionResult TryHandle(RecordingRequest request)
    {
        if (_isHandlingRequest)
        {
            throw new InvalidOperationException("A permission or lifecycle callback cannot send a nested request.");
        }

        _isHandlingRequest = true;
        try
        {
            return _session is { IsActive: true } session
                ? HandleSessionRequest(session, request)
                : HandleIdleRequest(request);
        }
        finally
        {
            _isHandlingRequest = false;
        }
    }

    /// <summary>Checks both Start policies before lazily creating and entering the session.</summary>
    /// <param name="request">The request received in Idle.</param>
    /// <returns>The request outcome.</returns>
    private TransitionResult HandleIdleRequest(RecordingRequest request)
    {
        if (request != RecordingRequest.Start)
        {
            return TransitionResult.InvalidRequest;
        }

        if (!_canStartRecording() || !_canEnterSession())
        {
            return TransitionResult.GuardDenied;
        }

        RecordingSessionState session = _session ??= new RecordingSessionState(_canExitSession, _reportActivity);
        _idleState.Exit(_reportActivity);
        session.Enter(new RecordingState());
        return TransitionResult.Transitioned;
    }

    /// <summary>Switches children freely, or checks exit permission before Stop leaves the composite.</summary>
    /// <param name="session">The active parent.</param>
    /// <param name="request">The action requested within the session.</param>
    /// <returns>The request outcome.</returns>
    private TransitionResult HandleSessionRequest(RecordingSessionState session, RecordingRequest request)
    {
        IRecordingSubstate? targetSubstate = (session.ActiveMode, request) switch
        {
            (RecordingMode.Recording, RecordingRequest.Pause) => new PausedState(),
            (RecordingMode.Paused, RecordingRequest.Resume) => new RecordingState(),
            _ => null
        };

        if (targetSubstate is not null)
        {
            session.ChangeSubstate(targetSubstate);
            return TransitionResult.Transitioned;
        }

        if (request != RecordingRequest.Stop)
        {
            return TransitionResult.InvalidRequest;
        }

        if (!session.CanExit())
        {
            return TransitionResult.GuardDenied;
        }

        session.Exit();
        _idleState.Enter(_reportActivity);
        return TransitionResult.Transitioned;
    }
}
