namespace RecordingWithHierarchy;

/// <summary>Defines the leaf-state behavior contained by the Recording Session composite.</summary>
internal interface IRecordingSubstate
{
    /// <summary>Gets the observable leaf mode represented by this substate.</summary>
    RecordingMode Mode { get; }

    /// <summary>Runs after this child becomes active.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    void Enter(Action<string> reportActivity);

    /// <summary>Runs before this child is replaced or its parent session exits.</summary>
    /// <param name="reportActivity">Receives lifecycle activity synchronously.</param>
    void Exit(Action<string> reportActivity);
}
