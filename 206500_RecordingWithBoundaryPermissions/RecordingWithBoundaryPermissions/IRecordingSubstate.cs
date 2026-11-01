namespace RecordingWithBoundaryPermissions;

/// <summary>Defines lifecycle behavior for an exclusive child of RecordingSession.</summary>
internal interface IRecordingSubstate
{
    /// <summary>Gets the child's observable mode.</summary>
    RecordingMode Mode { get; }

    /// <summary>Runs after the child becomes active.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    void Enter(Action<string> reportActivity);

    /// <summary>Runs before replacing the child or leaving its parent.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    void Exit(Action<string> reportActivity);
}
