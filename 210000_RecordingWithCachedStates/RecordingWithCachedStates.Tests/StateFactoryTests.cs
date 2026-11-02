namespace RecordingWithCachedStates.Tests;

/// <summary>Verifies that each recorder-local factory lazily caches its state instances.</summary>
[TestClass]
public sealed class StateFactoryTests
{
    /// <summary>
    /// Repeated requests for each mode return the same reference, while requesting another mode
    /// yields a different state. This is per-factory reuse, not a process-wide singleton.
    /// </summary>
    /// <param name="mode">The state mode to request repeatedly.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle)]
    [DataRow(RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused)]
    public void Get_ReusesSameInstanceForMode(RecordingMode mode)
    {
        // Arrange
        var factory = new StateFactory(() => true, () => true);

        // Act
        IRecorderState first = factory.Get(mode);
        IRecorderState second = factory.Get(mode);
        RecordingMode otherMode = mode == RecordingMode.Idle ? RecordingMode.Recording : RecordingMode.Idle;
        IRecorderState other = factory.Get(otherMode);

        // Assert
        Assert.AreSame(first, second);
        Assert.AreNotSame(first, other);
        Assert.AreEqual(mode, first.Mode);
    }

    /// <summary>
    /// Separate factories produce separate Recording and Paused instances that retain their own
    /// policies. Reusing an instance within a recorder therefore cannot leak callbacks across recorders.
    /// </summary>
    [TestMethod]
    public void SeparateFactories_KeepSeparateInstancesAndPolicies()
    {
        // Arrange
        var firstFactory = new StateFactory(() => false, () => false);
        var secondFactory = new StateFactory(() => true, () => true);
        IRecorderState firstRecording = firstFactory.Get(RecordingMode.Recording);
        IRecorderState secondRecording = secondFactory.Get(RecordingMode.Recording);
        IRecorderState firstPaused = firstFactory.Get(RecordingMode.Paused);
        IRecorderState secondPaused = secondFactory.Get(RecordingMode.Paused);

        // Act
        bool firstCanExit = firstRecording.CanExit();
        bool secondCanExit = secondRecording.CanExit();
        bool firstCanEnter = firstPaused.CanEnter();
        bool secondCanEnter = secondPaused.CanEnter();

        // Assert
        Assert.AreNotSame(firstRecording, secondRecording);
        Assert.AreNotSame(firstPaused, secondPaused);
        Assert.IsFalse(firstCanExit);
        Assert.IsTrue(secondCanExit);
        Assert.IsFalse(firstCanEnter);
        Assert.IsTrue(secondCanEnter);
    }

    /// <summary>
    /// A recorder returns to its original state objects after complete cycles, while repeating each
    /// state's permission check and lifecycle hooks. Reuse changes allocation, not transition behavior.
    /// </summary>
    [TestMethod]
    public void Recorder_ReusesInstancesAcrossTransitions()
    {
        // Arrange
        int exitChecks = 0;
        int entryChecks = 0;
        var trace = new List<string>();
        var recorder = new Recorder(() => true,
            () => { exitChecks++; return true; },
            () => { entryChecks++; return true; }, trace.Add);
        StateFactory factory = GetFactory(recorder);
        IRecorderState idle = factory.Get(RecordingMode.Idle);
        IRecorderState recording = factory.Get(RecordingMode.Recording);
        IRecorderState paused = factory.Get(RecordingMode.Paused);

        // Act
        TransitionResult[] results =
        [
            recorder.TryHandle(RecordingRequest.Start),
            recorder.TryHandle(RecordingRequest.Pause),
            recorder.TryHandle(RecordingRequest.Resume),
            recorder.TryHandle(RecordingRequest.Stop),
            recorder.TryHandle(RecordingRequest.Start),
            recorder.TryHandle(RecordingRequest.Pause),
            recorder.TryHandle(RecordingRequest.Stop)
        ];

        // Assert
        CollectionAssert.AreEqual(Enumerable.Repeat(TransitionResult.Transitioned, results.Length).ToArray(), results);
        Assert.AreSame(idle, factory.Get(RecordingMode.Idle));
        Assert.AreSame(recording, factory.Get(RecordingMode.Recording));
        Assert.AreSame(paused, factory.Get(RecordingMode.Paused));
        Assert.AreEqual(3, exitChecks);
        Assert.AreEqual(2, entryChecks);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle",
            "Exit Idle", "Enter Recording",
            "Exit Recording", "Enter Paused",
            "Exit Paused", "Enter Recording",
            "Exit Recording", "Enter Idle",
            "Exit Idle", "Enter Recording",
            "Exit Recording", "Enter Paused",
            "Exit Paused", "Enter Idle"
        }, trace);
    }

    private static StateFactory GetFactory(Recorder recorder)
    {
        var field = typeof(Recorder).GetField("_stateFactory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return (StateFactory)field.GetValue(recorder)!;
    }
}
