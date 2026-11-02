namespace RecordingWithStatePermissions.Tests;

/// <summary>Contrasts state-wide entry/exit permissions with the guard on just Idle + Start.</summary>
[TestClass]
public sealed class RecorderPermissionTests
{
    /// <summary>Initial Idle entry reports one hook without evaluating any application policy.</summary>
    [TestMethod]
    public void Constructor_EntersIdleWithoutChecks()
    {
        // Arrange
        var trace = new List<string>();
        Func<bool> unexpected = () => throw new InvalidOperationException("Unexpected permission check.");

        // Act
        var recorder = new Recorder(unexpected, unexpected, unexpected, trace.Add);

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, trace);
    }

    /// <summary>
    /// Exercises all five accepted transitions. The exact trace proves that Start alone uses its
    /// guard, both departures from Recording use CanExit, and only arrival at Paused uses CanEnter.
    /// Policy and exit callbacks see the source mode; entry callbacks see the selected target.
    /// </summary>
    /// <param name="source">The initial mode reached through the public API.</param>
    /// <param name="request">The accepted request.</param>
    /// <param name="target">The expected mode after completion.</param>
    /// <param name="expectedTrace">The expected ordered policy and lifecycle messages.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording,
        "Start guard (Idle)|Exit Idle (Idle)|Enter Recording (Recording)")]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused,
        "Recording.CanExit (Recording)|Paused.CanEnter (Recording)|Exit Recording (Recording)|Enter Paused (Paused)")]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle,
        "Recording.CanExit (Recording)|Exit Recording (Recording)|Enter Idle (Idle)")]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording,
        "Exit Paused (Paused)|Enter Recording (Recording)")]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle,
        "Exit Paused (Paused)|Enter Idle (Idle)")]
    public void AcceptedTransition_ChecksApplicablePoliciesBeforeHooks(
        RecordingMode source, RecordingRequest request, RecordingMode target, string expectedTrace)
    {
        // Arrange
        var trace = new List<string>();
        Recorder? recorder = null;
        recorder = new Recorder(
            () => { trace.Add($"Start guard ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"Recording.CanExit ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"Paused.CanEnter ({recorder?.CurrentState})"); return true; },
            message => trace.Add($"{message} ({recorder?.CurrentState})"));
        Reach(recorder, source);
        trace.Clear();

        // Act
        TransitionResult result = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(target, recorder.CurrentState);
        CollectionAssert.AreEqual(expectedTrace.Split('|'), trace);
    }

    /// <summary>
    /// Undefined pairs, including unknown numeric requests in every state, return InvalidRequest
    /// without evaluating permissions or running hooks. A missing transition differs from denial.
    /// </summary>
    /// <param name="source">The mode receiving the invalid request.</param>
    /// <param name="request">A request with no transition in that mode.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Start)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Start)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, (RecordingRequest)999)]
    [DataRow(RecordingMode.Recording, (RecordingRequest)999)]
    [DataRow(RecordingMode.Paused, (RecordingRequest)999)]
    public void UndefinedTransition_DoesNotCheckPermissions(RecordingMode source, RecordingRequest request)
    {
        // Arrange
        bool rejectCallbacks = false;
        Func<bool> policy = () => rejectCallbacks
            ? throw new InvalidOperationException("Unexpected policy.") : true;
        var trace = new List<string>();
        var recorder = new Recorder(policy, policy, policy, trace.Add);
        Reach(recorder, source);
        trace.Clear();
        rejectCallbacks = true;

        // Act
        TransitionResult result = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.InvalidRequest, result);
        Assert.AreEqual(source, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);
    }

    /// <summary>
    /// A denied Start guard leaves Idle unchanged with no hooks. A later allowed Start reevaluates
    /// the guard and enters Recording, without consulting policies for leaving Recording or entering Paused.
    /// </summary>
    [TestMethod]
    public void StartGuard_DeniesThenAllowsRetry()
    {
        // Arrange
        bool allowed = false;
        int checks = 0;
        var trace = new List<string>();
        Func<bool> unexpected = () => throw new InvalidOperationException("Unrelated state permission.");
        var recorder = new Recorder(() => { checks++; return allowed; }, unexpected, unexpected, trace.Add);
        trace.Clear();

        // Act
        TransitionResult denied = recorder.TryHandle(RecordingRequest.Start);

        // Assert
        Assert.AreEqual(TransitionResult.GuardDenied, denied);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(1, checks);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);

        // Arrange
        allowed = true;

        // Act
        TransitionResult accepted = recorder.TryHandle(RecordingRequest.Start);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, accepted);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        Assert.AreEqual(2, checks);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter Recording" }, trace);
    }

    /// <summary>
    /// Recording.CanExit denies both Pause and Stop. No lifecycle hook runs, and Pause must not
    /// evaluate Paused.CanEnter after exit denial. Changing the exit policy permits the same request on retry.
    /// </summary>
    /// <param name="request">An outgoing Recording transition.</param>
    /// <param name="target">The mode reached after permission is restored.</param>
    [TestMethod]
    [DataRow(RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingRequest.Stop, RecordingMode.Idle)]
    public void RecordingCanExit_RestrictsEveryDeparture(RecordingRequest request, RecordingMode target)
    {
        // Arrange
        bool allowed = false;
        var trace = new List<string>();
        var recorder = new Recorder(() => true,
            () => { trace.Add("CanExit"); return allowed; },
            () =>
            {
                trace.Add("CanEnter");
                if (!allowed) throw new InvalidOperationException("Entry checked after exit denial.");
                return true;
            }, trace.Add);
        Reach(recorder, RecordingMode.Recording);
        trace.Clear();

        // Act
        TransitionResult denied = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.GuardDenied, denied);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExit" }, trace);

        // Arrange
        allowed = true;
        trace.Clear();

        // Act
        TransitionResult accepted = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, accepted);
        Assert.AreEqual(target, recorder.CurrentState);
        CollectionAssert.AreEqual(request == RecordingRequest.Pause
            ? new[] { "CanExit", "CanEnter", "Exit Recording", "Enter Paused" }
            : new[] { "CanExit", "Exit Recording", "Enter Idle" }, trace);
    }

    /// <summary>
    /// Paused.CanEnter can deny Pause even after Recording.CanExit succeeds. Neither lifecycle
    /// hook runs on denial; a later Pause rechecks both policies and succeeds when entry is allowed.
    /// </summary>
    [TestMethod]
    public void PausedCanEnter_DeniesBeforeSourceExit()
    {
        // Arrange
        bool allowed = false;
        var trace = new List<string>();
        var recorder = new Recorder(() => true,
            () => { trace.Add("CanExit"); return true; },
            () => { trace.Add("CanEnter"); return allowed; }, trace.Add);
        Reach(recorder, RecordingMode.Recording);
        trace.Clear();

        // Act
        TransitionResult denied = recorder.TryHandle(RecordingRequest.Pause);

        // Assert
        Assert.AreEqual(TransitionResult.GuardDenied, denied);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExit", "CanEnter" }, trace);

        // Arrange
        allowed = true;
        trace.Clear();

        // Act
        TransitionResult accepted = recorder.TryHandle(RecordingRequest.Pause);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, accepted);
        Assert.AreEqual(RecordingMode.Paused, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExit", "CanEnter", "Exit Recording", "Enter Paused" }, trace);
    }

    /// <summary>
    /// After reaching Paused, all injected policies are set to false or to throw. Resume still enters
    /// Recording: it uses neither the Start-only guard, Recording's exit policy, nor Paused's entry policy.
    /// </summary>
    /// <param name="throwOnCheck">Whether an accidental policy invocation throws instead of returning false.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Resume_DoesNotReuseUnrelatedChecks(bool throwOnCheck)
    {
        // Arrange
        bool allowed = true;
        int checks = 0;
        Func<bool> policy = () =>
        {
            checks++;
            if (!allowed && throwOnCheck) throw new InvalidOperationException("Unrelated policy.");
            return allowed;
        };
        var trace = new List<string>();
        var recorder = new Recorder(policy, policy, policy, trace.Add);
        Reach(recorder, RecordingMode.Paused);
        trace.Clear();
        checks = 0;
        allowed = false;

        // Act
        TransitionResult result = recorder.TryHandle(RecordingRequest.Resume);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        Assert.AreEqual(0, checks);
        CollectionAssert.AreEqual(new[] { "Exit Paused", "Enter Recording" }, trace);
    }

    /// <summary>
    /// Exceptions from each application policy propagate as the same exception before any lifecycle
    /// hook. The source remains active, and disabling the exception allows a retry, proving processing-flag recovery.
    /// </summary>
    /// <param name="policyName">The throwing policy.</param>
    [TestMethod]
    [DataRow("Start")]
    [DataRow("CanExit")]
    [DataRow("CanEnter")]
    public void PolicyFailure_LeavesSourceAndAllowsRetry(string policyName)
    {
        // Arrange
        bool shouldThrow = false;
        var failure = new InvalidOperationException("Policy failed.");
        bool Check(string name)
        {
            if (shouldThrow && name == policyName) throw failure;
            return true;
        }
        var trace = new List<string>();
        var recorder = new Recorder(() => Check("Start"), () => Check("CanExit"), () => Check("CanEnter"), trace.Add);
        RecordingMode source = policyName == "Start" ? RecordingMode.Idle : RecordingMode.Recording;
        RecordingRequest request = policyName == "Start" ? RecordingRequest.Start : RecordingRequest.Pause;
        Reach(recorder, source);
        trace.Clear();
        shouldThrow = true;

        // Act & Assert
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(request));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(source, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);

        // Arrange
        shouldThrow = false;

        // Act
        TransitionResult result = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(policyName == "Start" ? RecordingMode.Recording : RecordingMode.Paused, recorder.CurrentState);
        CollectionAssert.AreEqual(policyName == "Start"
            ? new[] { "Exit Idle", "Enter Recording" } : new[] { "Exit Recording", "Enter Paused" }, trace);
    }

    /// <summary>
    /// Nested requests are rejected from the transition guard, either state policy, and a lifecycle
    /// callback. Permission failures have no lifecycle trace; the exit callback fails after recording
    /// Exit Recording but before state replacement. Turning off nesting allows the request to be retried.
    /// </summary>
    /// <param name="callback">The callback that attempts the nested request.</param>
    [TestMethod]
    [DataRow("Start")]
    [DataRow("CanExit")]
    [DataRow("CanEnter")]
    [DataRow("Exit Recording")]
    public void NestedRequest_ThrowsAndAllowsRetry(string callback)
    {
        // Arrange
        bool nested = false;
        Recorder? recorder = null;
        bool Check(string name)
        {
            if (nested && callback == name) recorder!.TryHandle(RecordingRequest.Start);
            return true;
        }
        var trace = new List<string>();
        recorder = new Recorder(() => Check("Start"), () => Check("CanExit"), () => Check("CanEnter"), message =>
        {
            trace.Add(message);
            if (nested && callback == message) recorder!.TryHandle(RecordingRequest.Start);
        });
        RecordingMode source = callback == "Start" ? RecordingMode.Idle : RecordingMode.Recording;
        RecordingRequest request = callback == "Start" ? RecordingRequest.Start : RecordingRequest.Pause;
        Reach(recorder, source);
        trace.Clear();
        nested = true;

        // Act & Assert
        var failure = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(request));
        Assert.AreEqual("A permission or lifecycle callback cannot send a nested request.", failure.Message);
        Assert.AreEqual(source, recorder.CurrentState);
        CollectionAssert.AreEqual(callback == "Exit Recording" ? new[] { "Exit Recording" } : Array.Empty<string>(), trace);

        // Arrange
        nested = false;
        trace.Clear();

        // Act
        TransitionResult result = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(callback == "Start" ? RecordingMode.Recording : RecordingMode.Paused, recorder.CurrentState);
        CollectionAssert.AreEqual(callback == "Start"
            ? new[] { "Exit Idle", "Enter Recording" } : new[] { "Exit Recording", "Enter Paused" }, trace);
    }

    /// <summary>
    /// After all Pause permissions pass, an exit exception retains Recording, while a Paused entry
    /// exception retains the already-selected Paused state. The original exception and exact trace
    /// demonstrate that lifecycle failure is neither a denied result nor a rolled-back transition.
    /// </summary>
    /// <param name="activity">The hook that throws.</param>
    /// <param name="expectedMode">The selected state after failure.</param>
    [TestMethod]
    [DataRow("Exit Recording", RecordingMode.Recording)]
    [DataRow("Enter Paused", RecordingMode.Paused)]
    public void LifecycleFailure_PreservesNoRollbackContract(string activity, RecordingMode expectedMode)
    {
        // Arrange
        var failure = new InvalidOperationException("Lifecycle failed.");
        var trace = new List<string>();
        var recorder = new Recorder(() => true, () => true, () => true, message =>
        {
            trace.Add(message);
            if (message == activity) throw failure;
        });
        Reach(recorder, RecordingMode.Recording);
        trace.Clear();

        // Act & Assert
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(expectedMode, recorder.CurrentState);
        CollectionAssert.AreEqual(activity == "Exit Recording"
            ? new[] { "Exit Recording" } : new[] { "Exit Recording", "Enter Paused" }, trace);
    }

    /// <summary>Every missing constructor callback is rejected immediately with its parameter name.</summary>
    /// <param name="parameter">The omitted callback.</param>
    [TestMethod]
    [DataRow("canStartRecording")]
    [DataRow("canLeaveRecording")]
    [DataRow("canEnterPaused")]
    [DataRow("reportActivity")]
    public void NullCallback_ThrowsWithParameterName(string parameter)
    {
        // Arrange
        Func<bool>? start = parameter == "canStartRecording" ? null : () => true;
        Func<bool>? exit = parameter == "canLeaveRecording" ? null : () => true;
        Func<bool>? entry = parameter == "canEnterPaused" ? null : () => true;
        Action<string>? report = parameter == "reportActivity" ? null : _ => { };

        // Act & Assert
        var failure = Assert.ThrowsExactly<ArgumentNullException>(() => new Recorder(start!, exit!, entry!, report!));
        Assert.AreEqual(parameter, failure.ParamName);
    }

    /// <summary>One recorder's denied exit does not affect another recorder's policies, active state, or hooks.</summary>
    [TestMethod]
    public void Instances_KeepSeparateStatesAndPolicies()
    {
        // Arrange
        var firstTrace = new List<string>();
        var secondTrace = new List<string>();
        var first = new Recorder(() => true, () => false, () => true, firstTrace.Add);
        var second = new Recorder(() => true, () => true, () => true, secondTrace.Add);
        Reach(first, RecordingMode.Recording);
        Reach(second, RecordingMode.Recording);
        firstTrace.Clear();
        secondTrace.Clear();

        // Act
        TransitionResult firstResult = first.TryHandle(RecordingRequest.Stop);
        TransitionResult secondResult = second.TryHandle(RecordingRequest.Stop);

        // Assert
        Assert.AreEqual(TransitionResult.GuardDenied, firstResult);
        Assert.AreEqual(RecordingMode.Recording, first.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), firstTrace);
        Assert.AreEqual(TransitionResult.Transitioned, secondResult);
        Assert.AreEqual(RecordingMode.Idle, second.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Recording", "Enter Idle" }, secondTrace);
    }

    /// <summary>Reaches a test's source state through accepted public requests with setup policies allowed.</summary>
    /// <param name="recorder">The initially Idle recorder.</param>
    /// <param name="mode">The required source mode.</param>
    private static void Reach(Recorder recorder, RecordingMode mode)
    {
        if (mode == RecordingMode.Idle) return;
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        if (mode == RecordingMode.Paused)
        {
            Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        }
    }
}
