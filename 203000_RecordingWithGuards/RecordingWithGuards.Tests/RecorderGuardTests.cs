namespace RecordingWithGuards.Tests;

/// <summary>Verifies transition guards, lifecycle ordering, and the original recorder contract.</summary>
[TestClass]
public sealed class RecorderGuardTests
{
    /// <summary>Construction enters Idle once without consulting Start permission.</summary>
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

    /// <summary>Accepted pairs check the applicable guard before exit and entry, with correct active modes.</summary>
    /// <param name="source">The active mode before the request.</param>
    /// <param name="request">The accepted request.</param>
    /// <param name="target">The expected resulting mode.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle)]
    public void AcceptedRequest_RunsOnlyApplicableGuardBeforeLifecycle(
        RecordingMode source, RecordingRequest request, RecordingMode target)
    {
        var trace = new List<string>();
        Recorder? recorder = null;
        bool permission = true;
        recorder = new Recorder(
            () => { trace.Add($"Guard ({recorder?.CurrentState})"); return permission; },
            activity => trace.Add($"{activity} ({recorder?.CurrentState})"));
        Reach(recorder, source);
        trace.Clear();
        permission = source == RecordingMode.Idle;

        Assert.IsTrue(recorder.TryHandle(request));
        Assert.AreEqual(target, recorder.CurrentState);
        var expected = new List<string>();
        if (source == RecordingMode.Idle)
        {
            expected.Add("Guard (Idle)");
        }
        expected.Add($"Exit {source} ({source})");
        expected.Add($"Enter {target} ({target})");
        CollectionAssert.AreEqual(expected, trace);
    }

    /// <summary>Invalid state/request pairs evaluate no guard and execute no lifecycle hooks.</summary>
    /// <param name="source">The mode in which the request is invalid.</param>
    /// <param name="request">The rejected request.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Start)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Start)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Pause)]
    public void InvalidRequest_DoesNotEvaluateGuardOrRunHooks(RecordingMode source, RecordingRequest request)
    {
        var activities = new List<string>();
        int guardCalls = 0;
        var recorder = new Recorder(() => { guardCalls++; return true; }, activities.Add);
        Reach(recorder, source);
        activities.Clear();
        guardCalls = 0;

        Assert.IsFalse(recorder.TryHandle(request));
        Assert.AreEqual(source, recorder.CurrentState);
        Assert.AreEqual(0, guardCalls);
        CollectionAssert.AreEqual(Array.Empty<string>(), activities);
    }

    /// <summary>A false guard rejects Start without exiting Idle or entering Recording.</summary>
    [TestMethod]
    public void DeniedStart_LeavesIdleWithoutLifecycleActivity()
    {
        var activities = new List<string>();
        int guardCalls = 0;
        var recorder = new Recorder(() => { guardCalls++; return false; }, activities.Add);
        activities.Clear();

        Assert.IsFalse(recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(1, guardCalls);
        CollectionAssert.AreEqual(Array.Empty<string>(), activities);
    }

    /// <summary>Every Start attempt in Idle reevaluates permission, including after a completed recording.</summary>
    [TestMethod]
    public void Start_RetriesWithFreshPermissionAndDoesNotCacheSuccess()
    {
        bool permission = false;
        int guardCalls = 0;
        var activities = new List<string>();
        var recorder = new Recorder(() => { guardCalls++; return permission; }, activities.Add);

        Assert.IsFalse(recorder.TryHandle(RecordingRequest.Start));
        permission = true;
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Stop));
        permission = false;
        Assert.IsFalse(recorder.TryHandle(RecordingRequest.Start));

        Assert.AreEqual(3, guardCalls);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter Recording", "Exit Recording", "Enter Idle"
        }, activities);
    }

    /// <summary>Revisiting Recording through Resume does not call the Start guard even when permission changes.</summary>
    [TestMethod]
    public void Resume_IgnoresStartPermissionAndPreservesFullLifecycleTrace()
    {
        bool permission = true;
        int guardCalls = 0;
        var activities = new List<string>();
        var recorder = new Recorder(() => { guardCalls++; return permission; }, activities.Add);

        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Pause));
        permission = false;
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Resume));
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Stop));

        Assert.AreEqual(1, guardCalls);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter Recording", "Exit Recording", "Enter Paused",
            "Exit Paused", "Enter Recording", "Exit Recording", "Enter Idle"
        }, activities);
    }

    /// <summary>Guard exceptions propagate before lifecycle work and do not prevent a later retry.</summary>
    [TestMethod]
    public void ThrowingGuard_PreservesStateAndAllowsLaterRequest()
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
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(2, guardCalls);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter Recording" }, activities);
    }

    /// <summary>Both constructor dependencies are required and validated by parameter name.</summary>
    /// <param name="nullGuard">Whether to omit the permission check rather than the activity callback.</param>
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

    /// <summary>Nested requests from either callback are rejected and the outer processing flag resets.</summary>
    /// <param name="fromGuard">Whether the nested request originates in the guard rather than source exit.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NestedRequest_IsRejectedAndLaterRequestCanSucceed(bool fromGuard)
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
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter Recording" }, activities);
    }

    /// <summary>Lifecycle failure preserves the previous example's exception and no-rollback contract.</summary>
    /// <param name="failingActivity">The lifecycle hook that throws.</param>
    /// <param name="expectedMode">The active mode remaining after failure.</param>
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
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        activities.Clear();

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(expectedMode, recorder.CurrentState);
        CollectionAssert.AreEqual(failingActivity == "Exit Recording"
            ? new[] { "Exit Recording" }
            : new[] { "Exit Recording", "Enter Paused" }, activities);
    }

    /// <summary>Separate recorders have independent modes, permissions, and activity sinks.</summary>
    [TestMethod]
    public void Instances_DoNotShareStateOrPermission()
    {
        var firstActivities = new List<string>();
        var secondActivities = new List<string>();
        var first = new Recorder(() => true, firstActivities.Add);
        var second = new Recorder(() => false, secondActivities.Add);

        Assert.IsTrue(first.TryHandle(RecordingRequest.Start));
        Assert.IsFalse(second.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, first.CurrentState);
        Assert.AreEqual(RecordingMode.Idle, second.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle", "Exit Idle", "Enter Recording" }, firstActivities);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, secondActivities);
    }

    /// <summary>Reaches a test source state using only accepted public requests.</summary>
    /// <param name="recorder">The initially idle recorder with Start permission granted.</param>
    /// <param name="mode">The desired source mode.</param>
    private static void Reach(Recorder recorder, RecordingMode mode)
    {
        if (mode != RecordingMode.Idle)
        {
            Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        }
        if (mode == RecordingMode.Paused)
        {
            Assert.IsTrue(recorder.TryHandle(RecordingRequest.Pause));
        }
        Assert.AreEqual(mode, recorder.CurrentState);
    }
}
