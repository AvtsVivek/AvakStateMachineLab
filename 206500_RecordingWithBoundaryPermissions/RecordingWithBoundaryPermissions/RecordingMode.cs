namespace RecordingWithBoundaryPermissions;

/// <summary>Identifies the active leaf mode, with Idle outside the recording session.</summary>
public enum RecordingMode
{
    /// <summary>No session is active.</summary>
    Idle,
    /// <summary>The session's Recording child is active.</summary>
    Recording,
    /// <summary>The session's Paused child is active.</summary>
    Paused
}
