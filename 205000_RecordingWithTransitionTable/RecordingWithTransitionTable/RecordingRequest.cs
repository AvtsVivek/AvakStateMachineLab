namespace RecordingWithTransitionTable;

/// <summary>Identifies requests whose meaning depends on the active mode.</summary>
public enum RecordingRequest
{
    /// <summary>Requests a new recording from Idle, subject to the Start guard.</summary>
    Start,
    /// <summary>Requests that an active recording pause.</summary>
    Pause,
    /// <summary>Requests that a paused recording continue without the Start guard.</summary>
    Resume,
    /// <summary>Requests that an active or paused recording end.</summary>
    Stop
}
