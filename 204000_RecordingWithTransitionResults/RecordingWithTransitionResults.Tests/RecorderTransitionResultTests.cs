namespace RecordingWithTransitionResults.Tests;

/// <summary>Verifies result distinctions alongside state changes and lifecycle activity.</summary>
[TestClass]
public sealed class RecorderTransitionResultTests
{
    /// <summary>Construction enters Idle without consulting Start permission.</summary>
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

    /// <summary>Every defined transition returns Transitioned and preserves guard and lifecycle order.</summary>
    /// <param name="source">The active source mode.</param>
    /// <param name="request">The accepted request.</param>
    /// <param name="target">The target mode after completion.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle)]
    public void AcceptedRequest_ReturnsTransitionedWithOrderedActivity(
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

    /// <summary>Every undefined state/request pair returns InvalidRequest without guard or lifecycle work.</summary>
    /// <param name="source">The state in which the request is undefined.</param>
    /// <param name="request">The invalid request.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Start)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Start)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Pause)]
    public void InvalidPair_ReturnsInvalidRequestWithoutSideEffects(RecordingMode source, RecordingRequest request)
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

    /// <summary>A valid Start rejected by policy returns GuardDenied, not InvalidRequest.</summary>
    [TestMethod]
    public void DeniedStart_ReturnsGuardDeniedAndLeavesIdleUntouched()
    {
        var activities = new List<string>();
        int guardCalls = 0;
        var recorder = new Recorder(() => { guardCalls++; return false; }, activities.Add);
        activities.Clear();

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(1, guardCalls);
        CollectionAssert.AreEqual(Array.Empty<string>(), activities);
    }

    /// <summary>A rejected Start can be retried and each attempt reevaluates permission.</summary>
    [TestMethod]
    public void Start_RetryReturnsDistinctResultsForCurrentPermission()
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

    /// <summary>Resume returns Transitioned even after Start permission is withdrawn.</summary>
    [TestMethod]
    public void Resume_ReturnsTransitionedWithoutRecheckingStartGuard()
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

    /// <summary>Guard exceptions propagate and are not converted to GuardDenied.</summary>
    [TestMethod]
    public void ThrowingGuard_PropagatesWithoutResultOrLifecycleActivity()
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

    /// <summary>Null callbacks are rejected with the corresponding parameter name.</summary>
    /// <param name="nullGuard">Whether to pass a null guard rather than a null activity callback.</param>
    /// <param name="parameterName">The expected exception parameter name.</param>
    [TestMethod]
    [DataRow(true, "canStartRecording")]
    [DataRow(false, "reportActivity")]
    public void NullCallback_IsRejected(bool nullGuard, string parameterName)
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() =>
            new Recorder(nullGuard ? null! : () => true, nullGuard ? _ => { } : null!));

        Assert.AreEqual(parameterName, exception.ParamName);
    }

    /// <summary>Nested requests from guards and lifecycle callbacks throw and leave the recorder usable.</summary>
    /// <param name="fromGuard">True when the nested request originates in the guard.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NestedRequest_ThrowsAndAllowsSubsequentTransition(bool fromGuard)
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

    /// <summary>Lifecycle exceptions propagate with the same no-rollback state behavior as the prior exercise.</summary>
    /// <param name="failingActivity">The lifecycle activity that throws.</param>
    /// <param name="expectedMode">The active mode after the failure.</param>
    [TestMethod]
    [DataRow("Exit Recording", RecordingMode.Recording)]
    [DataRow("Enter Paused", RecordingMode.Paused)]
    public void LifecycleFailure_PropagatesWithoutRollback(string failingActivity, RecordingMode expectedMode)
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

    /// <summary>Instances retain independent state, guard, and activity behavior.</summary>
    [TestMethod]
    public void Instances_DoNotShareStateOrPermission()
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

    /// <summary>Reaches a source mode through accepted public requests.</summary>
    /// <param name="recorder">An initially idle recorder with permission granted.</param>
    /// <param name="mode">The desired source mode.</param>
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
