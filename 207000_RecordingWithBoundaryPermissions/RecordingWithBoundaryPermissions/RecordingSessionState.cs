namespace RecordingWithBoundaryPermissions;

/// <summary>Owns one active child and parent lifecycle hooks, with exit permission scoped to leaving the session.</summary>
[System.Diagnostics.DebuggerDisplay("ActiveSubstate = {_activeSubstate?.Mode.ToString(),nq}, IsActive = {IsActive}")]
internal sealed class RecordingSessionState
{
    private readonly Func<bool> _canExit;
    private readonly Action<string> _reportActivity;
    private IRecordingSubstate? _activeSubstate;

    /// <summary>Creates an inactive session without running hooks or evaluating permission.</summary>
    /// <param name="canExit">Checks permission to cross the boundary back to Idle, not to switch children.</param>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    public RecordingSessionState(Func<bool> canExit, Action<string> reportActivity)
    {
        _canExit = canExit;
        _reportActivity = reportActivity;
    }

    /// <summary>Gets whether this composite has an active child.</summary>
    public bool IsActive => _activeSubstate is not null;

    /// <summary>Gets the active leaf mode, or Idle when the session is inactive.</summary>
    public RecordingMode ActiveMode => _activeSubstate?.Mode ?? RecordingMode.Idle;

    /// <summary>Evaluates permission to leave this composite through Stop.</summary>
    /// <returns>Whether leaving the parent boundary is permitted.</returns>
    public bool CanExit() => _canExit();

    /// <summary>Activates the initial child, reports parent entry, then runs child entry.</summary>
    /// <param name="initialSubstate">The child selected for the new session.</param>
    public void Enter(IRecordingSubstate initialSubstate)
    {
        ArgumentNullException.ThrowIfNull(initialSubstate);
        _activeSubstate = initialSubstate;
        _reportActivity("Enter RecordingSession");
        _activeSubstate.Enter(_reportActivity);
    }

    /// <summary>Exits and replaces the child without parent permission checks or parent hooks.</summary>
    /// <param name="targetSubstate">The new active child.</param>
    public void ChangeSubstate(IRecordingSubstate targetSubstate)
    {
        ArgumentNullException.ThrowIfNull(targetSubstate);
        if (_activeSubstate is null)
        {
            throw new InvalidOperationException("Cannot change a child while RecordingSession is inactive.");
        }

        _activeSubstate.Exit(_reportActivity);
        _activeSubstate = targetSubstate;
        _activeSubstate.Enter(_reportActivity);
    }

    /// <summary>Exits the child and then the parent, deactivating the session after both hooks complete.</summary>
    public void Exit()
    {
        if (_activeSubstate is null)
        {
            throw new InvalidOperationException("Cannot exit RecordingSession while it is inactive.");
        }

        _activeSubstate.Exit(_reportActivity);
        _reportActivity("Exit RecordingSession");
        _activeSubstate = null;
    }
}
