namespace RecordingWithXmlDefinedTriggers;

internal sealed class RecordingState(Func<bool> canLeaveRecording) : RecorderStateBase
{
    public override RecordingMode Mode => RecordingMode.Recording;

    public override bool CanExit() => canLeaveRecording();
}
