namespace RecordingWithTransitionTable;

/// <summary>Describes whether a request transitioned, was invalid, or failed its applicable guard.</summary>
public enum TransitionResult
{
    /// <summary>The request matched a table row, its guard passed if applicable, and lifecycle work completed.</summary>
    Transitioned,
    /// <summary>The source/request pair has no row in the transition table.</summary>
    InvalidRequest,
    /// <summary>A matching transition exists, but its guard denied permission.</summary>
    GuardDenied
}
