namespace RecordingWithTransitionTable;

/// <summary>Defines lifecycle behavior for a recorder mode without owning transition rules.</summary>
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
}
