namespace RecordingWithSourceAwareTriggers;

/// <summary>Defines state-wide permissions separately from lifecycle actions.</summary>
internal interface IRecorderState
{
    RecordingMode Mode { get; }
    bool CanEnter();
    bool CanExit();
    void Enter(Action<string> reportActivity);
    void Exit(Action<string> reportActivity);
}
