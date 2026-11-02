namespace RecordingWithCachedStates;

/// <summary>Distinguishes completion, an undefined transition, and a denied condition.</summary>
public enum TransitionResult
{
    /// <summary>The transition and its lifecycle hooks completed.</summary>
    Transitioned,
    /// <summary>No transition exists for the state and request.</summary>
    InvalidRequest,
    /// <summary>A transition guard or state permission returned false before lifecycle work.</summary>
    GuardDenied
}
