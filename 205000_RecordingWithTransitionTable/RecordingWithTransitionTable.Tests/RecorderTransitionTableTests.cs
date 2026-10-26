namespace RecordingWithTransitionTable.Tests;

/// <summary>Verifies centralized transition rules, result values, and lifecycle behavior.</summary>
[TestClass]
public sealed class RecorderTransitionTableTests
{
    /// <summary>Construction enters Idle once without consulting the guarded table row.</summary>
    [TestMethod]
    public void Constructor_EntersIdleWithoutEvaluatingGuard()
    {
        var activities = new List<string>();
        int guardCalls = 0;
        var recorder = new Recorder(() => { guardCalls++; return false; }, activities.Add);

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(0, guardCalls);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activities);
    }

    /// <summary>All five table rows transition to their declared targets in guard/lifecycle order.</summary>
    /// <param name="source">The source mode listed in the transition table.</param>
    /// <param name="request">The request key for the transition row.</param>
    /// <param name="target">The target mode listed in the row.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle)]
    public void TableRow_ReturnsTransitionedAndPreservesExecutionOrder(
        RecordingMode source, RecordingRequest request, RecordingMode target)
    {
        var trace = new List<string>();
        Recorder? recorder = null;
        int guardCalls = 0;
        recorder = new Recorder(
            () => { guardCalls++; trace.Add($"Guard ({recorder?.CurrentState})"); return true; },
            activity => trace.Add($"{activity} ({recorder?.CurrentState})"));
        Reach(recorder, source);
        trace.Clear();
        guardCalls = 0;

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(request));
        Assert.AreEqual(target, recorder.CurrentState);
        Assert.AreEqual(source == RecordingMode.Idle ? 1 : 0, guardCalls);
        var expected = new List<string>();
        if (source == RecordingMode.Idle)
        {
            expected.Add("Guard (Idle)");
        }
        expected.Add($"Exit {source} ({source})");
        expected.Add($"Enter {target} ({target})");
        CollectionAssert.AreEqual(expected, trace);
    }

    /// <summary>All undefined source/request pairs return InvalidRequest without side effects.</summary>
    /// <param name="source">The source mode.</param>
    /// <param name="request">A request for which that mode has no table row.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Start)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Start)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Pause)]
    public void MissingTableRow_ReturnsInvalidRequestWithoutEffects(RecordingMode source, RecordingRequest request)
    {
        var activities = new List<string>();
        int guardCalls = 0;
        var recorder = new Recorder(() => { guardCalls++; return true; }, activities.Add);
        Reach(recorder, source);
        activities.Clear();
        guardCalls = 0;

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(request));
        Assert.AreEqual(source, recorder.CurrentState);
        Assert.AreEqual(0, guardCalls);
        CollectionAssert.AreEqual(Array.Empty<string>(), activities);
    }

    /// <summary>An undefined numeric request value is treated as a missing table row.</summary>
    [TestMethod]
    public void UnknownRequest_ReturnsInvalidRequest()
    {
        var activities = new List<string>();
        var recorder = new Recorder(() => true, activities.Add);
        activities.Clear();

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle((RecordingRequest)999));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), activities);
    }

    /// <summary>A matching guarded row with false permission returns GuardDenied before lifecycle work.</summary>
    [TestMethod]
    public void StartGuardDenied_ReturnsDistinctResultAndLeavesIdleActive()
    {
        int guardCalls = 0;
        var activities = new List<string>();
        var recorder = new Recorder(() => { guardCalls++; return false; }, activities.Add);
        activities.Clear();

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(1, guardCalls);
        CollectionAssert.AreEqual(Array.Empty<string>(), activities);
    }

    /// <summary>The guarded row reevaluates permission after denial and after returning to Idle.</summary>
    [TestMethod]
    public void Start_RetryUsesFreshPermission()
    {
        bool permission = false;
        int guardCalls = 0;
        var activities = new List<string>();
        var recorder = new Recorder(() => { guardCalls++; return permission; }, activities.Add);

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        permission = true;
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Stop));
        permission = false;
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));

        Assert.AreEqual(3, guardCalls);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter Recording", "Exit Recording", "Enter Idle"
        }, activities);
    }

    /// <summary>The unguarded Resume row succeeds even when Start permission is false.</summary>
    [TestMethod]
    public void Resume_IsNotGuardedByStartPermission()
    {
        bool permission = true;
        int guardCalls = 0;
        var activities = new List<string>();
        var recorder = new Recorder(() => { guardCalls++; return permission; }, activities.Add);

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        permission = false;
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Stop));

        Assert.AreEqual(1, guardCalls);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter Recording", "Exit Recording", "Enter Paused",
            "Exit Paused", "Enter Recording", "Exit Recording", "Enter Idle"
        }, activities);
    }

    /// <summary>A guard exception propagates and is not represented by GuardDenied.</summary>
    [TestMethod]
    public void ThrowingGuard_PropagatesBeforeLifecycleAndPermitsRetry()
    {
        var failure = new InvalidOperationException("Permission service failed.");
        bool shouldThrow = true;
        int guardCalls = 0;
        var activities = new List<string>();
        var recorder = new Recorder(() =>
        {
            guardCalls++;
            if (shouldThrow)
            {
                throw failure;
            }
            return true;
        }, activities.Add);
        activities.Clear();

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), activities);

        shouldThrow = false;
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(2, guardCalls);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter Recording" }, activities);
    }

    /// <summary>Both injected delegates are required.</summary>
    /// <param name="nullGuard">Whether the permission check is null.</param>
    /// <param name="parameterName">Expected exception parameter name.</param>
    [TestMethod]
    [DataRow(true, "canStartRecording")]
    [DataRow(false, "reportActivity")]
    public void NullDependency_ThrowsArgumentNullException(bool nullGuard, string parameterName)
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() =>
            new Recorder(nullGuard ? null! : () => true, nullGuard ? _ => { } : null!));
        Assert.AreEqual(parameterName, exception.ParamName);
    }

    /// <summary>Nested requests from the guarded row or source exit are rejected; processing resets afterward.</summary>
    /// <param name="fromGuard">Whether the nested request originates in the permission check.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NestedRequest_ThrowsAndLaterTransitionWorks(bool fromGuard)
    {
        Recorder? recorder = null;
        bool sendNested = true;
        var activities = new List<string>();
        recorder = new Recorder(() =>
        {
            if (sendNested && fromGuard)
            {
                recorder!.TryHandle(RecordingRequest.Start);
            }
            return true;
        }, activity =>
        {
            activities.Add(activity);
            if (sendNested && !fromGuard && activity == "Exit Idle")
            {
                recorder!.TryHandle(RecordingRequest.Start);
            }
        });
        activities.Clear();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual("A guard or lifecycle callback cannot send a nested request.", exception.Message);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(fromGuard ? Array.Empty<string>() : new[] { "Exit Idle" }, activities);

        sendNested = false;
        activities.Clear();
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter Recording" }, activities);
    }

    /// <summary>Lifecycle exceptions propagate and retain the established no-rollback behavior.</summary>
    /// <param name="failingActivity">The hook that throws.</param>
    /// <param name="expectedMode">The active mode after failure.</param>
    [TestMethod]
    [DataRow("Exit Recording", RecordingMode.Recording)]
    [DataRow("Enter Paused", RecordingMode.Paused)]
    public void LifecycleException_PropagatesWithoutRollback(string failingActivity, RecordingMode expectedMode)
    {
        var failure = new InvalidOperationException("Lifecycle failed.");
        var activities = new List<string>();
        var recorder = new Recorder(() => true, activity =>
        {
            activities.Add(activity);
            if (activity == failingActivity)
            {
                throw failure;
            }
        });
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        activities.Clear();

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(expectedMode, recorder.CurrentState);
        CollectionAssert.AreEqual(failingActivity == "Exit Recording"
            ? new[] { "Exit Recording" }
            : new[] { "Exit Recording", "Enter Paused" }, activities);
    }

    /// <summary>Recorder instances do not share active modes, guard behavior, or activity sinks.</summary>
    [TestMethod]
    public void Instances_KeepIndependentModesAndGuards()
    {
        var firstActivities = new List<string>();
        var secondActivities = new List<string>();
        var first = new Recorder(() => true, firstActivities.Add);
        var second = new Recorder(() => false, secondActivities.Add);

        Assert.AreEqual(TransitionResult.Transitioned, first.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.GuardDenied, second.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, first.CurrentState);
        Assert.AreEqual(RecordingMode.Idle, second.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle", "Exit Idle", "Enter Recording" }, firstActivities);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, secondActivities);
    }

    /// <summary>Reaches a state through accepted requests to prepare a test scenario.</summary>
    /// <param name="recorder">An initially idle recorder whose guard permits Start.</param>
    /// <param name="mode">The source mode to reach.</param>
    private static void Reach(Recorder recorder, RecordingMode mode)
    {
        if (mode != RecordingMode.Idle)
        {
            Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        }
        if (mode == RecordingMode.Paused)
        {
            Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        }
        Assert.AreEqual(mode, recorder.CurrentState);
    }
}
