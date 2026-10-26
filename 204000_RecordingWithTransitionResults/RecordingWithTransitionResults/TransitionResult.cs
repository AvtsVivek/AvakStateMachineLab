namespace RecordingWithTransitionResults;

/// <summary>Describes whether a request transitioned, was invalid, or failed its applicable guard.</summary>
public enum TransitionResult
{
    /// <summary>The request was valid, its guard passed if applicable, and lifecycle work completed.</summary>
    Transitioned,
    /// <summary>The active state does not define a transition for the request.</summary>
    InvalidRequest,
    /// <summary>The request identifies a transition, but its guard denied permission.</summary>
    GuardDenied
}
