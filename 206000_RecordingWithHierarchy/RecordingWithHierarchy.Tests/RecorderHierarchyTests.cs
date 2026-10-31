namespace RecordingWithHierarchy.Tests;

/// <summary>Verifies composite-state lifecycle order, child transitions, and inherited parent permissions.</summary>
[TestClass]
public sealed class RecorderHierarchyTests
{
    /// <summary>
    /// Verifies the recorder's initial lifecycle: constructing a recorder makes Idle the active mode
    /// and reports exactly one Enter Idle event. None of the Start, parent-entry, or parent-exit
    /// permission callbacks runs, because construction is not a requested transition.
    /// </summary>
    [TestMethod]
    public void Constructor_EntersIdleWithoutCheckingPermissions()
    {
        // Arrange
        var trace = new List<string>();
        int startCalls = 0;
        int enterCalls = 0;
        int exitCalls = 0;

        // Act
        var recorder = new Recorder(
            () => { startCalls++; return true; },
            () => { enterCalls++; return true; },
            () => { exitCalls++; return true; },
            trace.Add);

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(0, startCalls);
        Assert.AreEqual(0, enterCalls);
        Assert.AreEqual(0, exitCalls);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, trace);
    }

    /// <summary>
    /// Covers all five accepted state/request combinations with permissions allowed. Each request
    /// must return Transitioned and reach its expected leaf mode. The exact trace verifies that
    /// permissions run before hooks, Start enters the parent before its child, Stop exits the child
    /// before the parent, and Pause/Resume switch children without parent lifecycle events.
    /// Callback observations also verify the source mode during exit and the target mode during entry.
    /// </summary>
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
        // Arrange
        var trace = new List<string>();
        Recorder? recorder = null;
        recorder = new Recorder(
            () => { trace.Add($"CanStart ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"CanEnterSession ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"CanExitSession ({recorder?.CurrentState})"); return true; },
            activity => trace.Add($"{activity} ({recorder?.CurrentState})"));
        Reach(recorder, source);
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(request));

        // Assert
        Assert.AreEqual(target, recorder.CurrentState);
        CollectionAssert.AreEqual(ExpectedTransitionTrace(source, request, target), trace);
    }

    /// <summary>
    /// Covers the seven combinations of known modes and requests that have no defined transition.
    /// After reaching the source mode and clearing setup activity, the request must return
    /// InvalidRequest, preserve that mode, call none of the three permission callbacks, and produce
    /// no lifecycle events. This distinguishes an undefined request from a valid but denied transition.
    /// </summary>
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
        // Arrange
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

        // Act & Assert
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(request));

        // Assert
        Assert.AreEqual(source, recorder.CurrentState);
        Assert.AreEqual(0, startCalls);
        Assert.AreEqual(0, enterCalls);
        Assert.AreEqual(0, exitCalls);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);
    }

    /// <summary>
    /// Sends the numeric value 999, which is not a declared RecordingRequest, to an Idle recorder.
    /// The recorder must return InvalidRequest, remain Idle, and emit no lifecycle events rather
    /// than treating the unknown value as a valid transition trigger.
    /// </summary>
    [TestMethod]
    public void UnknownRequest_ReturnsInvalidRequest()
    {
        // Arrange
        var trace = new List<string>();
        var recorder = CreateRecorder(trace);
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle((RecordingRequest)999));

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);
    }

    /// <summary>
    /// Requests Start while the Start-specific guard returns false. The result must be GuardDenied
    /// and Idle must remain active. Only the Start check appears in the trace: the parent-entry
    /// policy is not called and no lifecycle hook runs. This verifies short-circuiting at the first denial.
    /// </summary>
    [TestMethod]
    public void StartGuardDenied_LeavesIdleWithoutParentEntryCheck()
    {
        // Arrange
        var trace = new List<string>();
        int enterCalls = 0;
        var recorder = new Recorder(
            () => { trace.Add("CanStart"); return false; },
            () => { enterCalls++; return true; },
            () => true,
            trace.Add);
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(0, enterCalls);
        CollectionAssert.AreEqual(new[] { "CanStart" }, trace);
    }

    /// <summary>
    /// Allows the Start-specific guard but denies the RecordingSession parent's entry policy.
    /// Start must return GuardDenied and leave Idle active. The trace contains Start permission
    /// followed by parent-entry permission only, proving that neither Idle exit nor parent/child
    /// entry runs when the second permission check denies the transition.
    /// </summary>
    [TestMethod]
    public void ParentEntryDeniedOnStart_LeavesIdleUntouched()
    {
        // Arrange
        var trace = new List<string>();
        var recorder = new Recorder(
            () => { trace.Add("CanStart"); return true; },
            () => { trace.Add("CanEnterSession"); return false; },
            () => { trace.Add("CanExitSession"); return true; },
            trace.Add);
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanStart", "CanEnterSession" }, trace);
    }

    /// <summary>
    /// Starts recording successfully, then changes the parent-entry policy to deny entry before Pause.
    /// Pause must check parent-exit permission and then parent-entry permission, return GuardDenied,
    /// and keep the Recording child active without lifecycle events. This demonstrates that parent
    /// permission applies to entering a sibling child even though the parent itself would stay active.
    /// </summary>
    [TestMethod]
    public void ParentEntryDeniedOnPause_LeavesRecordingChildActive()
    {
        // Arrange
        var trace = new List<string>();
        bool canEnter = true;
        var recorder = new Recorder(() => true, () => { trace.Add("CanEnterSession"); return canEnter; },
            () => { trace.Add("CanExitSession"); return true; }, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        trace.Clear();
        canEnter = false;

        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Pause));

        // Assert
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExitSession", "CanEnterSession" }, trace);
    }

    /// <summary>
    /// Starts recording, then denies the parent's exit policy before requesting Pause or Stop.
    /// Both requests must return GuardDenied and preserve the Recording child. Only the exit check
    /// is traced; no subsequent entry check or lifecycle hook runs. The two cases cover blocking
    /// both a child-only change and a transition that would leave the entire composite.
    /// </summary>
    /// <param name="request">A valid request departing from the active Recording child.</param>
    [TestMethod]
    [DataRow(RecordingRequest.Pause)]
    [DataRow(RecordingRequest.Stop)]
    public void ParentExitDenied_BlocksTransitionWithoutLifecycle(RecordingRequest request)
    {
        // Arrange
        var trace = new List<string>();
        bool canExit = true;
        var recorder = new Recorder(() => true, () => true, () => { trace.Add("CanExitSession"); return canExit; }, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        trace.Clear();
        canExit = false;

        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(request));

        // Assert
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExitSession" }, trace);
    }

    /// <summary>
    /// Starts and pauses a recording, then withdraws permission to start a new recording.
    /// Resume must still return Transitioned and activate Recording because the parent policies
    /// allow it. The Start guard's call count remains one, and the trace contains only Exit Paused
    /// and Enter Recording. This keeps the Start-specific guard separate from resuming a session.
    /// </summary>
    [TestMethod]
    public void Resume_DoesNotRecheckStartPermission()
    {
        // Arrange
        var trace = new List<string>();
        int startCalls = 0;
        bool canStart = true;
        var recorder = new Recorder(() => { startCalls++; return canStart; }, () => true, () => true, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        canStart = false;
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));

        // Assert
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        Assert.AreEqual(1, startCalls);
        CollectionAssert.AreEqual(new[] { "Exit Paused", "Enter Recording" }, trace);
    }

    /// <summary>
    /// Runs Start, Pause, Resume, and Stop with all permissions allowed and verifies the complete
    /// ordered trace, including initial Idle entry and every permission check. The parent enters
    /// once on Start, remains active through both child changes, and exits once after its child
    /// on Stop. All requests succeed and the final mode is Idle, demonstrating a full session lifecycle.
    /// </summary>
    [TestMethod]
    public void FullCycle_ParentLifecycleWrapsChildLifecycle()
    {
        // Arrange
        var trace = new List<string>();
        var recorder = new Recorder(
            () => { trace.Add("CanStart"); return true; },
            () => { trace.Add("CanEnterSession"); return true; },
            () => { trace.Add("CanExitSession"); return true; },
            trace.Add);

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Stop));

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "CanStart", "CanEnterSession", "Exit Idle", "Enter RecordingSession", "Enter Recording",
            "CanExitSession", "CanEnterSession", "Exit Recording", "Enter Paused",
            "CanExitSession", "CanEnterSession", "Exit Paused", "Enter Recording",
            "CanExitSession", "Exit Recording", "Exit RecordingSession", "Enter Idle"
        }, trace);
    }

    /// <summary>
    /// After starting a session, resets the policy counters and performs Pause followed by Resume.
    /// Both parent policies must be evaluated twice, once per child transition, rather than reusing
    /// earlier permission results. The exact lifecycle trace contains only the two child exit/entry
    /// pairs, showing that repeated parent permission checks do not re-enter or exit the parent.
    /// </summary>
    [TestMethod]
    public void ChildTransitions_ReevaluateParentPoliciesButKeepParentActive()
    {
        // Arrange
        var trace = new List<string>();
        int canEnterCalls = 0;
        int canExitCalls = 0;
        var recorder = new Recorder(() => true,
            () => { canEnterCalls++; return true; },
            () => { canExitCalls++; return true; }, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        trace.Clear();
        canEnterCalls = canExitCalls = 0;

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));

        // Assert
        Assert.AreEqual(2, canEnterCalls);
        Assert.AreEqual(2, canExitCalls);
        CollectionAssert.AreEqual(new[] { "Exit Recording", "Enter Paused", "Exit Paused", "Enter Recording" }, trace);
    }

    /// <summary>
    /// Makes the parent-entry policy throw during Start and verifies that the exact exception
    /// propagates rather than becoming GuardDenied. Idle remains active and no lifecycle event runs.
    /// After allowing the policy to succeed, a later Start must complete the normal parent/child
    /// entry sequence, proving that the request-processing flag resets after the failed attempt.
    /// </summary>
    [TestMethod]
    public void ParentPermissionException_PropagatesBeforeLifecycleAndAllowsRetry()
    {
        // Arrange
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

        // Act & Assert
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));

        // Assert
        Assert.AreSame(failure, actual);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);

        shouldThrow = false;
        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        // Assert
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter RecordingSession", "Enter Recording" }, trace);
    }

    /// <summary>
    /// Attempts to send another Start while an outer Start is already being processed, either from
    /// the parent-entry policy or the Exit Idle callback. The nested request must throw the expected
    /// InvalidOperationException and leave the mode Idle. The trace distinguishes failure before
    /// lifecycle work from failure during Idle exit. Disabling the nested request allows a later
    /// Start to succeed, verifying reentrancy protection and recovery of the processing flag.
    /// </summary>
    /// <param name="fromPermission">True to nest from parent permission, false to nest from lifecycle reporting.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NestedRequest_ThrowsAndLaterTransitionSucceeds(bool fromPermission)
    {
        // Arrange
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

        // Act & Assert
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));

        // Assert
        Assert.AreEqual("A permission or lifecycle callback cannot send a nested request.", exception.Message);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(fromPermission ? Array.Empty<string>() : new[] { "Exit Idle" }, activities);

        sendNested = false;
        activities.Clear();
        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        // Assert
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter RecordingSession", "Enter Recording" }, activities);
    }

    /// <summary>
    /// Omits each of the four constructor dependencies in turn: the Start guard, parent-entry
    /// policy, parent-exit policy, or lifecycle reporter. Construction must throw ArgumentNullException
    /// naming the omitted parameter. This verifies that even lazily used session dependencies are
    /// validated immediately rather than failing later when a recording is started.
    /// </summary>
    /// <param name="nullParameter">The parameter to omit.</param>
    [TestMethod]
    [DataRow("canStartRecording")]
    [DataRow("canEnterSession")]
    [DataRow("canExitSession")]
    [DataRow("reportActivity")]
    public void NullDependency_ThrowsWithParameterName(string nullParameter)
    {
        // Arrange
        Func<bool> canStart = nullParameter == "canStartRecording" ? null! : () => true;
        Func<bool> canEnter = nullParameter == "canEnterSession" ? null! : () => true;
        Func<bool> canExit = nullParameter == "canExitSession" ? null! : () => true;
        Action<string> report = nullParameter == "reportActivity" ? null! : _ => { };

        // Act & Assert
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new Recorder(canStart, canEnter, canExit, report));

        // Assert
        Assert.AreEqual(nullParameter, exception.ParamName);
    }

    /// <summary>
    /// Creates two recorders with separate activity lists and different parent-entry policies.
    /// Start succeeds on the first but is denied on the second. Their modes must remain Recording
    /// and Idle respectively, with only the first reporting session/child entry. This verifies that
    /// an instance's active hierarchy and permission behavior do not leak into another recorder.
    /// </summary>
    [TestMethod]
    public void Instances_DoNotShareCompositeState()
    {
        // Arrange
        var firstTrace = new List<string>();
        var secondTrace = new List<string>();
        var first = CreateRecorder(firstTrace);
        var second = new Recorder(() => true, () => false, () => true, secondTrace.Add);

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, first.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.GuardDenied, second.TryHandle(RecordingRequest.Start));

        // Assert
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
