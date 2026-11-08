namespace RecordingWithXmlTransitions;

/// <summary>Identifies the recorder's three flat states.</summary>
public enum RecordingMode
{
    /// <summary>No recording is in progress.</summary>
    Idle,
    /// <summary>A recording is in progress.</summary>
    Recording,
    /// <summary>The recording is temporarily paused.</summary>
    Paused
}
