namespace RecordingWithTransitionTable;

/// <summary>Provides a public observation of the active state without exposing state classes.</summary>
public enum RecordingMode
{
    /// <summary>No recording is in progress.</summary>
    Idle,
    /// <summary>Recording is in progress.</summary>
    Recording,
    /// <summary>An existing recording is paused.</summary>
    Paused
}
