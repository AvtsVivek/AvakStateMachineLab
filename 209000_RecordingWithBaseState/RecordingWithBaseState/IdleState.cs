namespace RecordingWithBaseState;

/// <summary>Supplies its mode and inherits unrestricted permissions and lifecycle reporting.</summary>
internal sealed class IdleState : RecorderStateBase
{
    /// <inheritdoc />
    public override RecordingMode Mode => RecordingMode.Idle;
}
