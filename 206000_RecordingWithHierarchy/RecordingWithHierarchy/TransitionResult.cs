namespace RecordingWithHierarchy;

/// <summary>Describes whether a request transitioned, was invalid, or was denied by policy.</summary>
public enum TransitionResult
{
    /// <summary>The request and all applicable policy checks succeeded and lifecycle work completed.</summary>
    Transitioned,
    /// <summary>The current leaf mode has no transition for the request.</summary>
    InvalidRequest,
    /// <summary>A request-specific or parent-level permission check denied the transition.</summary>
    GuardDenied
}
