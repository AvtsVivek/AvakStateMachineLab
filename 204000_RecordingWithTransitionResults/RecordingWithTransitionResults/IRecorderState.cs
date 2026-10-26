namespace RecordingWithTransitionResults;

/// <summary>Defines request handling and lifecycle behavior for one recorder state.</summary>
internal interface IRecorderState
{
    /// <summary>Gets the public mode represented by this state object.</summary>
    RecordingMode Mode { get; }

    /// <summary>Runs after this state becomes active, including initial Idle entry.</summary>
    /// <param name="reportActivity">Receives the entry activity synchronously.</param>
    void Enter(Action<string> reportActivity);

    /// <summary>Runs before this state is replaced by an accepted transition.</summary>
    /// <param name="reportActivity">Receives the exit activity synchronously.</param>
    void Exit(Action<string> reportActivity);

    /// <summary>Checks the request and applicable guard before selecting a target.</summary>
    /// <param name="recorder">The context that coordinates state replacement.</param>
    /// <param name="request">The requested transition trigger.</param>
    /// <returns>A result distinguishing transition, invalid request, and guard denial.</returns>
    TransitionResult TryHandle(Recorder recorder, RecordingRequest request);
}
