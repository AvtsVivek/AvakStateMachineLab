namespace RecordingWithSourceAwareTriggers;

internal sealed class IdleState : RecorderStateBase
{
    public override RecordingMode Mode => RecordingMode.Idle;
}
