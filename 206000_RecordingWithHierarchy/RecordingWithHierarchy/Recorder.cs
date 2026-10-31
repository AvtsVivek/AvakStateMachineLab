namespace RecordingWithHierarchy;

/// <summary>Coordinates a top-level Idle state and a composite Recording Session with child states.</summary>
/// <remarks>
/// This synchronous learning example is not thread-safe. Permission callbacks run before lifecycle
/// work. Callback exceptions propagate without rollback; callbacks must not send nested requests.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("CurrentState = {CurrentState}")]
public sealed class Recorder
{
    private readonly Func<bool> _canStartRecording;
    private readonly Func<bool> _canEnterSession;
    private readonly Func<bool> _canExitSession;
    private readonly Action<string> _reportActivity;
    private readonly IdleState _idleState = new();
    private RecordingSessionState? _session;
    private bool _isHandlingRequest;

    /// <summary>Creates an Idle recorder without allocating its session and enters Idle once.</summary>
    /// <param name="canStartRecording">A transition-specific permission check for starting a new session.</param>
    /// <param name="canEnterSession">The parent composite's entry permission, checked for every arrival to a child.</param>
    /// <param name="canExitSession">The parent composite's exit permission, checked for every departure from a child.</param>
    /// <param name="reportActivity">Receives parent and child lifecycle activity synchronously.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    public Recorder(
        Func<bool> canStartRecording,
        Func<bool> canEnterSession,
        Func<bool> canExitSession,
        Action<string> reportActivity)
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

    /// <summary>Gets the active leaf mode, or Idle before the session is created or while it is inactive.</summary>
    public RecordingMode CurrentState => _session?.ActiveMode ?? _idleState.Mode;

    /// <summary>Handles a request using parent permissions and lifecycle-aware hierarchical transitions.</summary>
    /// <param name="request">The requested recorder action.</param>
    /// <returns>Transitioned, InvalidRequest, or GuardDenied.</returns>
    /// <exception cref="InvalidOperationException">A permission or lifecycle callback sends a nested request.</exception>
    /// <remarks>Exceptions propagate and are not converted to result values.</remarks>
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

    /// <summary>Checks Start permissions before lazily creating and entering the session.</summary>
    /// <param name="request">The request received while Idle.</param>
    /// <returns>The outcome of the request.</returns>
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

        RecordingSessionState session = _session ??= new RecordingSessionState(
            _canEnterSession, _canExitSession, _reportActivity);
        _idleState.Exit(_reportActivity);
        session.Enter(new RecordingState());
        return TransitionResult.Transitioned;
    }

    /// <summary>Handles child changes or Stop within an active session.</summary>
    /// <param name="session">The active composite state.</param>
    /// <param name="request">The requested action.</param>
    /// <returns>The outcome of the request.</returns>
    private TransitionResult HandleSessionRequest(RecordingSessionState session, RecordingRequest request)
    {
        IRecordingSubstate? targetSubstate = (session.ActiveMode, request) switch
        {
            (RecordingMode.Recording, RecordingRequest.Pause) => new PausedState(),
            (RecordingMode.Paused, RecordingRequest.Resume) => new RecordingState(),
            _ => null
        };
        bool stopsSession = request == RecordingRequest.Stop
            && session.ActiveMode is RecordingMode.Recording or RecordingMode.Paused;

        if (targetSubstate is null && !stopsSession)
        {
            return TransitionResult.InvalidRequest;
        }

        if (!session.CanExit())
        {
            return TransitionResult.GuardDenied;
        }

        if (targetSubstate is not null)
        {
            if (!session.CanEnter())
            {
                return TransitionResult.GuardDenied;
            }

            session.ChangeSubstate(targetSubstate);
            return TransitionResult.Transitioned;
        }

        session.Exit();
        _idleState.Enter(_reportActivity);
        return TransitionResult.Transitioned;
    }
}
