namespace RecordingWithBoundaryPermissions;

/// <summary>Identifies the recorder's transition triggers.</summary>
public enum RecordingRequest
{
    /// <summary>Starts a session from Idle.</summary>
    Start,
    /// <summary>Changes the active child from Recording to Paused.</summary>
    Pause,
    /// <summary>Changes the active child from Paused to Recording.</summary>
    Resume,
    /// <summary>Ends the active session and returns to Idle.</summary>
    Stop
}
