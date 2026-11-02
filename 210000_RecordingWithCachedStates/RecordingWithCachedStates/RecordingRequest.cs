namespace RecordingWithCachedStates;

/// <summary>Identifies actions whose meaning depends on the current state.</summary>
public enum RecordingRequest
{
    /// <summary>Begins a new recording from Idle.</summary>
    Start,
    /// <summary>Pauses an active recording.</summary>
    Pause,
    /// <summary>Continues a paused recording.</summary>
    Resume,
    /// <summary>Ends an active or paused recording.</summary>
    Stop
}
