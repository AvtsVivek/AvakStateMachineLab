namespace RecordingWithSourceAwareTriggers;

/// <summary>Supplies unrestricted permissions and common lifecycle reporting.</summary>
[System.Diagnostics.DebuggerDisplay("{Mode.ToString(),nq}")]
internal abstract class RecorderStateBase : IRecorderState
{
    public abstract RecordingMode Mode { get; }

    public virtual bool CanEnter() => true;

    public virtual bool CanExit() => true;

    public virtual void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    public virtual void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
