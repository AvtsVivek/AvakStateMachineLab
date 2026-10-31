namespace RecordingWithHierarchy;

/// <summary>Coordinates a top-level Idle state and a composite Recording Session with child states.</summary>
/// <remarks>
/// This synchronous learning example is not thread-safe. Permission callbacks run before lifecycle
/// work. Callback exceptions propagate without rollback; callbacks must not send nested requests.
/// </remarks>
public sealed class Recorder
{
    private readonly Func<bool> _canStartRecording;
    private readonly Action<string> _reportActivity;
    private readonly IdleState _idleState = new();
    private readonly RecordingSessionState _session;
    private bool _isHandlingRequest;

    /// <summary>Creates an Idle recorder and enters it once without evaluating permission checks.</summary>
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
        _reportActivity = reportActivity;
        _session = new RecordingSessionState(canEnterSession, canExitSession, reportActivity);
        _idleState.Enter(_reportActivity);
    }

    /// <summary>Gets the active leaf mode; the parent session is observable through lifecycle activity.</summary>
    public RecordingMode CurrentState => _session.ActiveMode;

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
            return _session.IsActive
                ? HandleSessionRequest(request)
                : HandleIdleRequest(request);
        }
        finally
        {
            _isHandlingRequest = false;
        }
    }

    private TransitionResult HandleIdleRequest(RecordingRequest request)
    {
        if (request != RecordingRequest.Start)
        {
            return TransitionResult.InvalidRequest;
        }

        if (!_canStartRecording() || !_session.CanEnter())
        {
            return TransitionResult.GuardDenied;
        }

        _idleState.Exit(_reportActivity);
        _session.Enter(new RecordingState());
        return TransitionResult.Transitioned;
    }

    private TransitionResult HandleSessionRequest(RecordingRequest request)
    {
        IRecordingSubstate? targetSubstate = (_session.ActiveMode, request) switch
        {
            (RecordingMode.Recording, RecordingRequest.Pause) => new PausedState(),
            (RecordingMode.Paused, RecordingRequest.Resume) => new RecordingState(),
            _ => null
        };
        bool stopsSession = request == RecordingRequest.Stop
            && _session.ActiveMode is RecordingMode.Recording or RecordingMode.Paused;

        if (targetSubstate is null && !stopsSession)
        {
            return TransitionResult.InvalidRequest;
        }

        if (!_session.CanExit())
        {
            return TransitionResult.GuardDenied;
        }

        if (targetSubstate is not null)
        {
            if (!_session.CanEnter())
            {
                return TransitionResult.GuardDenied;
            }

            _session.ChangeSubstate(targetSubstate);
            return TransitionResult.Transitioned;
        }

        _session.Exit();
        _idleState.Enter(_reportActivity);
        return TransitionResult.Transitioned;
    }
}
