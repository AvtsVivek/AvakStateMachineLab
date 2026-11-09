namespace RecordingWithXmlDefinedGuards;

internal interface IRecorderState
{
    RecordingMode Mode { get; }
    bool CanEnter();
    bool CanExit();
    void Enter(Action<string> reportActivity);
    void Exit(Action<string> reportActivity);
}
