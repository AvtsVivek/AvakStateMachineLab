namespace RecordingWithHierarchy;

/// <summary>Identifies the currently active leaf mode of the recorder.</summary>
public enum RecordingMode
{
    /// <summary>No recording session is active.</summary>
    Idle,
    /// <summary>The active recording-session child is recording.</summary>
    Recording,
    /// <summary>The active recording-session child is paused.</summary>
    Paused
}
