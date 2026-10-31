namespace RecordingWithHierarchy;

/// <summary>Composite state containing exactly one active Recording or Paused child.</summary>
internal sealed class RecordingSessionState
{
    private readonly Func<bool> _canEnter;
    private readonly Func<bool> _canExit;
    private readonly Action<string> _reportActivity;
    private IRecordingSubstate? _activeSubstate;

    /// <summary>Creates the composite state with parent-level permission checks and activity reporting.</summary>
    /// <param name="canEnter">Checks permission to enter or remain within this composite.</param>
    /// <param name="canExit">Checks permission to leave or transition within this composite.</param>
    /// <param name="reportActivity">Receives parent and child lifecycle activity.</param>
    public RecordingSessionState(Func<bool> canEnter, Func<bool> canExit, Action<string> reportActivity)
    {
        _canEnter = canEnter;
        _canExit = canExit;
        _reportActivity = reportActivity;
    }

    /// <summary>Gets whether the parent composite is active.</summary>
    public bool IsActive => _activeSubstate is not null;

    /// <summary>Gets the active child mode, or Idle when the composite is inactive.</summary>
    public RecordingMode ActiveMode => _activeSubstate?.Mode ?? RecordingMode.Idle;

    /// <summary>Evaluates the parent-level entry permission.</summary>
    /// <returns>True when the composite permits entry.</returns>
    public bool CanEnter() => _canEnter();

    /// <summary>Evaluates the parent-level exit permission.</summary>
    /// <returns>True when the composite permits the transition.</returns>
    public bool CanExit() => _canExit();

    /// <summary>Enters the parent and then its initial child.</summary>
    /// <param name="initialSubstate">The initial child state.</param>
    public void Enter(IRecordingSubstate initialSubstate)
    {
        ArgumentNullException.ThrowIfNull(initialSubstate);
        _activeSubstate = initialSubstate;
        _reportActivity("Enter RecordingSession");
        _activeSubstate.Enter(_reportActivity);
    }

    /// <summary>Switches children while leaving the parent composite active.</summary>
    /// <param name="targetSubstate">The child state that replaces the active child.</param>
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

    /// <summary>Exits the active child before exiting and deactivating the parent.</summary>
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
