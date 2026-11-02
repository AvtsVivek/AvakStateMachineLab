namespace RecordingWithBoundaryPermissions;

/// <summary>Distinguishes completed transitions, undefined requests, and permission denial.</summary>
public enum TransitionResult
{
    /// <summary>The transition's lifecycle work completed.</summary>
    Transitioned,
    /// <summary>No transition exists for the active mode and request.</summary>
    InvalidRequest,
    /// <summary>A Start or parent-boundary permission denied the transition.</summary>
    GuardDenied
}
