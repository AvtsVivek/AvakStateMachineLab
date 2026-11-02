using System.Diagnostics;
using System.Reflection;

namespace RecordingWithCachedStates.Tests;

/// <summary>Verifies the shared defaults, virtual extension points, and inherited debugger metadata.</summary>
[TestClass]
public sealed class RecorderStateBaseTests
{
    /// <summary>
    /// A minimal subclass supplies only Mode. Through IRecorderState it inherits both true
    /// permissions and reports entry/exit using that mode, with no transition coordinator involved.
    /// </summary>
    /// <param name="mode">The concrete mode supplied by the minimal subclass.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle)]
    [DataRow(RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused)]
    public void MinimalSubclass_InheritsPermissionsAndModeBasedHooks(RecordingMode mode)
    {
        // Arrange
        IRecorderState state = new DefaultState(mode);
        var trace = new List<string>();

        // Act
        bool canEnter = state.CanEnter();
        bool canExit = state.CanExit();
        state.Enter(trace.Add);
        state.Exit(trace.Add);

        // Assert
        Assert.IsTrue(canEnter);
        Assert.IsTrue(canExit);
        Assert.AreEqual(mode, state.Mode);
        CollectionAssert.AreEqual(new[] { $"Enter {mode}", $"Exit {mode}" }, trace);
    }

    /// <summary>
    /// Concrete state overrides dispatch through IRecorderState and reevaluate changing policies.
    /// Recording retains default entry permission; Paused retains default exit permission. Each
    /// restricted method invokes only its own policy once per call, without caching the result.
    /// </summary>
    /// <param name="allowed">The first permission result; the second call must observe its inverse.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConcreteOverrides_ReevaluateOnlyTheirOwnPolicy(bool allowed)
    {
        // Arrange
        int exitChecks = 0;
        int entryChecks = 0;
        IRecorderState recording = new RecordingState(() => { exitChecks++; return allowed; });
        IRecorderState paused = new PausedState(() => { entryChecks++; return allowed; });

        // Act
        bool recordingEntry = recording.CanEnter();
        bool pausedExit = paused.CanExit();
        bool firstExit = recording.CanExit();
        bool firstEntry = paused.CanEnter();
        allowed = !allowed;
        bool secondExit = recording.CanExit();
        bool secondEntry = paused.CanEnter();

        // Assert
        Assert.IsTrue(recordingEntry);
        Assert.IsTrue(pausedExit);
        Assert.AreEqual(!allowed, firstExit);
        Assert.AreEqual(!allowed, firstEntry);
        Assert.AreEqual(allowed, secondExit);
        Assert.AreEqual(allowed, secondEntry);
        Assert.AreEqual(2, exitChecks);
        Assert.AreEqual(2, entryChecks);
    }

    /// <summary>
    /// A subclass can extend virtual lifecycle methods while explicitly preserving base reporting.
    /// Calls through the interface dispatch to the overrides, and base calls report the subclass's Mode.
    /// </summary>
    [TestMethod]
    public void LifecycleOverrides_CanExtendBaseReportingThroughInterface()
    {
        // Arrange
        IRecorderState state = new ExtendedState();
        var trace = new List<string>();

        // Act
        state.Enter(trace.Add);
        state.Exit(trace.Add);

        // Assert
        CollectionAssert.AreEqual(new[] { "Enter Paused", "Custom entry", "Custom exit", "Exit Paused" }, trace);
    }

    /// <summary>
    /// Each real leaf state derives from RecorderStateBase and inherits its single mode-only
    /// DebuggerDisplay attribute, rather than duplicating metadata on concrete classes. This checks
    /// attribute inheritance and the mode text; it does not automate Visual Studio's debugger UI.
    /// </summary>
    /// <param name="mode">The leaf state to construct.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle)]
    [DataRow(RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused)]
    public void ConcreteStates_InheritModeOnlyDebuggerDisplay(RecordingMode mode)
    {
        // Arrange
        var factory = new StateFactory(() => true, () => true);
        IRecorderState state = factory.Get(mode);

        // Act
        Type? baseType = state.GetType().BaseType;
        DebuggerDisplayAttribute? inherited = state.GetType().GetCustomAttribute<DebuggerDisplayAttribute>(inherit: true);
        DebuggerDisplayAttribute? declared = state.GetType().GetCustomAttribute<DebuggerDisplayAttribute>(inherit: false);

        // Assert
        Assert.AreEqual(typeof(RecorderStateBase), baseType);
        Assert.IsNotNull(inherited);
        Assert.AreEqual("{Mode.ToString(),nq}", inherited.Value);
        Assert.IsNull(declared);
        Assert.AreEqual(mode, state.Mode);
    }

    private sealed class DefaultState(RecordingMode mode) : RecorderStateBase
    {
        public override RecordingMode Mode => mode;
    }

    private sealed class ExtendedState : RecorderStateBase
    {
        public override RecordingMode Mode => RecordingMode.Paused;

        public override void Enter(Action<string> reportActivity)
        {
            base.Enter(reportActivity);
            reportActivity("Custom entry");
        }

        public override void Exit(Action<string> reportActivity)
        {
            reportActivity("Custom exit");
            base.Exit(reportActivity);
        }
    }
}
