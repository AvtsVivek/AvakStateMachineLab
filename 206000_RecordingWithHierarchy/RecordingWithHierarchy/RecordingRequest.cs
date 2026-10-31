namespace RecordingWithHierarchy;

/// <summary>Identifies recorder requests whose meaning depends on the active leaf mode.</summary>
public enum RecordingRequest
{
    /// <summary>Requests a new recording session from Idle.</summary>
    Start,
    /// <summary>Requests that the active recording child pause.</summary>
    Pause,
    /// <summary>Requests that the paused child resume recording.</summary>
    Resume,
    /// <summary>Requests that the active recording session end.</summary>
    Stop
}
