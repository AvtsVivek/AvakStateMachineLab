namespace RecordingWithBaseState;

/// <summary>Shares default permissions and lifecycle reporting without owning transition coordination.</summary>
/// <remarks>Code inheritance is not state-machine hierarchy: this base is never an active parent state.</remarks>
[System.Diagnostics.DebuggerDisplay("{Mode.ToString(),nq}")]
internal abstract class RecorderStateBase : IRecorderState
{
    /// <inheritdoc />
    public abstract RecordingMode Mode { get; }

    /// <summary>Allows entry unless a concrete state overrides the permission.</summary>
    /// <returns>True by default.</returns>
    public virtual bool CanEnter() => true;

    /// <summary>Allows exit unless a concrete state overrides the permission.</summary>
    /// <returns>True by default.</returns>
    public virtual bool CanExit() => true;

    /// <summary>Reports entry using the concrete state's mode.</summary>
    /// <param name="reportActivity">Receives entry activity synchronously.</param>
    public virtual void Enter(Action<string> reportActivity) => reportActivity($"Enter {Mode}");

    /// <summary>Reports exit using the concrete state's mode.</summary>
    /// <param name="reportActivity">Receives exit activity synchronously.</param>
    public virtual void Exit(Action<string> reportActivity) => reportActivity($"Exit {Mode}");
}
