namespace RecordingWithEntryAndExit;

/// <summary>
/// Defines state-dependent request handling and lifecycle actions.
/// </summary>
internal interface IRecorderState
{
    /// <summary>Gets the public mode represented by this state object.</summary>
    RecordingMode Mode { get; }

    /// <summary>Runs the state's entry action after it becomes active.</summary>
    /// <param name="reportActivity">The synchronous destination for lifecycle activity.</param>
    void Enter(Action<string> reportActivity);

    /// <summary>Runs the state's exit action while it is still active.</summary>
    /// <param name="reportActivity">The synchronous destination for lifecycle activity.</param>
    void Exit(Action<string> reportActivity);

    /// <summary>Handles a request, selecting a new state only when the request is allowed.</summary>
    /// <param name="recorder">The context whose active state may change.</param>
    /// <param name="request">The request acting as the trigger.</param>
    /// <returns>True for acceptance; false for rejection without a transition.</returns>
    bool TryHandle(Recorder recorder, RecordingRequest request);
}
