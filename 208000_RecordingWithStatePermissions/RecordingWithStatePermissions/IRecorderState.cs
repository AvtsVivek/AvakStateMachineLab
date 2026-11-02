namespace RecordingWithStatePermissions;

/// <summary>Defines state-wide permissions separately from lifecycle actions.</summary>
internal interface IRecorderState
{
    /// <summary>Gets the observable mode represented by this state.</summary>
    RecordingMode Mode { get; }

    /// <summary>Checks permission for any incoming transition before lifecycle work.</summary>
    /// <returns>Whether entering this state is permitted.</returns>
    bool CanEnter();

    /// <summary>Checks permission for any outgoing transition before lifecycle work.</summary>
    /// <returns>Whether leaving this state is permitted.</returns>
    bool CanExit();

    /// <summary>Runs after this state is selected, or during initial Idle entry.</summary>
    /// <param name="reportActivity">Receives entry activity synchronously.</param>
    void Enter(Action<string> reportActivity);

    /// <summary>Runs before this state is replaced.</summary>
    /// <param name="reportActivity">Receives exit activity synchronously.</param>
    void Exit(Action<string> reportActivity);
}
