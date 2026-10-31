namespace RecordingWithHierarchy.Tests;

/// <summary>Verifies composite-state lifecycle order, child transitions, and inherited parent permissions.</summary>
[TestClass]
public sealed class RecorderHierarchyTests
{
    /// <summary>Construction enters only Idle and does not evaluate transition or parent permissions.</summary>
    [TestMethod]
    public void Constructor_EntersIdleWithoutCheckingPermissions()
    {
        var trace = new List<string>();
        int startCalls = 0;
        int enterCalls = 0;
        int exitCalls = 0;
        var recorder = new Recorder(
            () => { startCalls++; return true; },
            () => { enterCalls++; return true; },
            () => { exitCalls++; return true; },
            trace.Add);

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(0, startCalls);
        Assert.AreEqual(0, enterCalls);
        Assert.AreEqual(0, exitCalls);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, trace);
    }

    /// <summary>Each accepted transition reports parent checks and hooks in their required order.</summary>
    /// <param name="source">The initial leaf mode.</param>
    /// <param name="request">An accepted request in that mode.</param>
    /// <param name="target">The resulting leaf mode.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle)]
    public void AcceptedTransition_ReportsExpectedChecksAndLifecycle(
        RecordingMode source, RecordingRequest request, RecordingMode target)
    {
        var trace = new List<string>();
        Recorder? recorder = null;
        recorder = new Recorder(
            () => { trace.Add($"CanStart ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"CanEnterSession ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"CanExitSession ({recorder?.CurrentState})"); return true; },
            activity => trace.Add($"{activity} ({recorder?.CurrentState})"));
        Reach(recorder, source);
        trace.Clear();

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(request));
        Assert.AreEqual(target, recorder.CurrentState);
        CollectionAssert.AreEqual(ExpectedTransitionTrace(source, request, target), trace);
    }

    /// <summary>Undefined state/request pairs return InvalidRequest without invoking guards or lifecycle hooks.</summary>
    /// <param name="source">The leaf mode.</param>
    /// <param name="request">A request not defined for that mode.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Start)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Start)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Pause)]
    public void InvalidRequest_DoesNotEvaluatePermissionsOrRunHooks(RecordingMode source, RecordingRequest request)
    {
        var trace = new List<string>();
        int startCalls = 0;
        int enterCalls = 0;
        int exitCalls = 0;
        var recorder = new Recorder(
            () => { startCalls++; return true; },
            () => { enterCalls++; return true; },
            () => { exitCalls++; return true; },
            trace.Add);
        Reach(recorder, source);
        trace.Clear();
        startCalls = enterCalls = exitCalls = 0;

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(request));
        Assert.AreEqual(source, recorder.CurrentState);
        Assert.AreEqual(0, startCalls);
        Assert.AreEqual(0, enterCalls);
        Assert.AreEqual(0, exitCalls);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);
    }

    /// <summary>An unknown request value has no hierarchy transition and returns InvalidRequest.</summary>
    [TestMethod]
    public void UnknownRequest_ReturnsInvalidRequest()
    {
        var trace = new List<string>();
        var recorder = CreateRecorder(trace);
        trace.Clear();

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle((RecordingRequest)999));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);
    }

    /// <summary>The Start-specific permission denial does not invoke parent entry permission or lifecycle actions.</summary>
    [TestMethod]
    public void StartGuardDenied_LeavesIdleWithoutParentEntryCheck()
    {
        var trace = new List<string>();
        int enterCalls = 0;
        var recorder = new Recorder(
            () => { trace.Add("CanStart"); return false; },
            () => { enterCalls++; return true; },
            () => true,
            trace.Add);
        trace.Clear();

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(0, enterCalls);
        CollectionAssert.AreEqual(new[] { "CanStart" }, trace);
    }

    /// <summary>Parent CanEnter denial blocks initial composite entry before any lifecycle hooks.</summary>
    [TestMethod]
    public void ParentEntryDeniedOnStart_LeavesIdleUntouched()
    {
        var trace = new List<string>();
        var recorder = new Recorder(
            () => { trace.Add("CanStart"); return true; },
            () => { trace.Add("CanEnterSession"); return false; },
            () => { trace.Add("CanExitSession"); return true; },
            trace.Add);
        trace.Clear();

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanStart", "CanEnterSession" }, trace);
    }

    /// <summary>Parent CanEnter is checked on child entry even though the parent lifecycle is not re-entered.</summary>
    [TestMethod]
    public void ParentEntryDeniedOnPause_LeavesRecordingChildActive()
    {
        var trace = new List<string>();
        bool canEnter = true;
        var recorder = new Recorder(() => true, () => { trace.Add("CanEnterSession"); return canEnter; },
            () => { trace.Add("CanExitSession"); return true; }, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        trace.Clear();
        canEnter = false;

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExitSession", "CanEnterSession" }, trace);
    }

    /// <summary>Parent CanExit denial blocks child changes and composite exit before lifecycle work.</summary>
    /// <param name="request">A valid request departing from the active Recording child.</param>
    [TestMethod]
    [DataRow(RecordingRequest.Pause)]
    [DataRow(RecordingRequest.Stop)]
    public void ParentExitDenied_BlocksTransitionWithoutLifecycle(RecordingRequest request)
    {
        var trace = new List<string>();
        bool canExit = true;
        var recorder = new Recorder(() => true, () => true, () => { trace.Add("CanExitSession"); return canExit; }, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        trace.Clear();
        canExit = false;

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(request));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExitSession" }, trace);
    }

    /// <summary>Start permission is specific to starting a session; Resume has only parent-level checks.</summary>
    [TestMethod]
    public void Resume_DoesNotRecheckStartPermission()
    {
        var trace = new List<string>();
        int startCalls = 0;
        bool canStart = true;
        var recorder = new Recorder(() => { startCalls++; return canStart; }, () => true, () => true, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        canStart = false;
        trace.Clear();

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        Assert.AreEqual(1, startCalls);
        CollectionAssert.AreEqual(new[] { "Exit Paused", "Enter Recording" }, trace);
    }

    /// <summary>A complete cycle enters and exits the parent once while switching only its child between.</summary>
    [TestMethod]
    public void FullCycle_ParentLifecycleWrapsChildLifecycle()
    {
        var trace = new List<string>();
        var recorder = new Recorder(
            () => { trace.Add("CanStart"); return true; },
            () => { trace.Add("CanEnterSession"); return true; },
            () => { trace.Add("CanExitSession"); return true; },
            trace.Add);

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Stop));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "CanStart", "CanEnterSession", "Exit Idle", "Enter RecordingSession", "Enter Recording",
            "CanExitSession", "CanEnterSession", "Exit Recording", "Enter Paused",
            "CanExitSession", "CanEnterSession", "Exit Paused", "Enter Recording",
            "CanExitSession", "Exit Recording", "Exit RecordingSession", "Enter Idle"
        }, trace);
    }

    /// <summary>Each child transition freshly reevaluates both parent permissions without parent lifecycle activity.</summary>
    [TestMethod]
    public void ChildTransitions_ReevaluateParentPoliciesButKeepParentActive()
    {
        var trace = new List<string>();
        int canEnterCalls = 0;
        int canExitCalls = 0;
        var recorder = new Recorder(() => true,
            () => { canEnterCalls++; return true; },
            () => { canExitCalls++; return true; }, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        trace.Clear();
        canEnterCalls = canExitCalls = 0;

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));
        Assert.AreEqual(2, canEnterCalls);
        Assert.AreEqual(2, canExitCalls);
        CollectionAssert.AreEqual(new[] { "Exit Recording", "Enter Paused", "Exit Paused", "Enter Recording" }, trace);
    }

    /// <summary>A parent permission exception propagates before hooks and a later request may retry.</summary>
    [TestMethod]
    public void ParentPermissionException_PropagatesBeforeLifecycleAndAllowsRetry()
    {
        var failure = new InvalidOperationException("Session entry policy failed.");
        bool shouldThrow = true;
        var trace = new List<string>();
        var recorder = new Recorder(() => true, () =>
        {
            if (shouldThrow)
            {
                throw failure;
            }
            return true;
        }, () => true, trace.Add);
        trace.Clear();

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);

        shouldThrow = false;
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter RecordingSession", "Enter Recording" }, trace);
    }

    /// <summary>Callbacks cannot issue nested requests; the recorder is usable after the exception.</summary>
    /// <param name="fromPermission">True to nest from parent permission, false to nest from lifecycle reporting.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NestedRequest_ThrowsAndLaterTransitionSucceeds(bool fromPermission)
    {
        Recorder? recorder = null;
        bool sendNested = true;
        var activities = new List<string>();
        recorder = new Recorder(() => true, () =>
        {
            if (sendNested && fromPermission)
            {
                recorder!.TryHandle(RecordingRequest.Start);
            }
            return true;
        }, () => true, activity =>
        {
            activities.Add(activity);
            if (sendNested && !fromPermission && activity == "Exit Idle")
            {
                recorder!.TryHandle(RecordingRequest.Start);
            }
        });
        activities.Clear();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual("A permission or lifecycle callback cannot send a nested request.", exception.Message);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(fromPermission ? Array.Empty<string>() : new[] { "Exit Idle" }, activities);

        sendNested = false;
        activities.Clear();
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter RecordingSession", "Enter Recording" }, activities);
    }

    /// <summary>All injected callbacks are required.</summary>
    /// <param name="nullParameter">The parameter to omit.</param>
    [TestMethod]
    [DataRow("canStartRecording")]
    [DataRow("canEnterSession")]
    [DataRow("canExitSession")]
    [DataRow("reportActivity")]
    public void NullDependency_ThrowsWithParameterName(string nullParameter)
    {
        Func<bool> canStart = nullParameter == "canStartRecording" ? null! : () => true;
        Func<bool> canEnter = nullParameter == "canEnterSession" ? null! : () => true;
        Func<bool> canExit = nullParameter == "canExitSession" ? null! : () => true;
        Action<string> report = nullParameter == "reportActivity" ? null! : _ => { };

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new Recorder(canStart, canEnter, canExit, report));

        Assert.AreEqual(nullParameter, exception.ParamName);
    }

    /// <summary>Separate recorders keep independent active children and parent policies.</summary>
    [TestMethod]
    public void Instances_DoNotShareCompositeState()
    {
        var firstTrace = new List<string>();
        var secondTrace = new List<string>();
        var first = CreateRecorder(firstTrace);
        var second = new Recorder(() => true, () => false, () => true, secondTrace.Add);

        Assert.AreEqual(TransitionResult.Transitioned, first.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.GuardDenied, second.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, first.CurrentState);
        Assert.AreEqual(RecordingMode.Idle, second.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle", "Exit Idle", "Enter RecordingSession", "Enter Recording" }, firstTrace);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, secondTrace);
    }

    private static Recorder CreateRecorder(List<string> trace)
        => new(() => true, () => true, () => true, trace.Add);

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

    private static string[] ExpectedTransitionTrace(RecordingMode source, RecordingRequest request, RecordingMode target)
    {
        var trace = new List<string>();
        if (request == RecordingRequest.Start)
        {
            trace.AddRange(new[] { "CanStart (Idle)", "CanEnterSession (Idle)", "Exit Idle (Idle)",
                "Enter RecordingSession (Recording)", "Enter Recording (Recording)" });
        }
        else
        {
            trace.Add($"CanExitSession ({source})");
            if (request is RecordingRequest.Pause or RecordingRequest.Resume)
            {
                trace.Add($"CanEnterSession ({source})");
                trace.Add($"Exit {source} ({source})");
                trace.Add($"Enter {target} ({target})");
            }
            else
            {
                trace.Add($"Exit {source} ({source})");
                trace.Add($"Exit RecordingSession ({source})");
                trace.Add("Enter Idle (Idle)");
            }
        }
        return trace.ToArray();
    }
}
