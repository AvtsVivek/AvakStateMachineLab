namespace RecordingWithXmlDefinedTriggers;

internal sealed class PausedState(Func<bool> canEnterPaused) : RecorderStateBase
{
    public override RecordingMode Mode => RecordingMode.Paused;

    public override bool CanEnter() => canEnterPaused();
}
