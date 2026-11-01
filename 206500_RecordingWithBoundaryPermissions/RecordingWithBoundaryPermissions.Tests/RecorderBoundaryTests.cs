namespace RecordingWithBoundaryPermissions.Tests;

/// <summary>Verifies boundary-only permissions alongside the exclusive hierarchy's lifecycle contract.</summary>
[TestClass]
public sealed class RecorderBoundaryTests
{
    /// <summary>Construction reports Idle entry only, without evaluating any of the three policies.</summary>
    [TestMethod]
    public void Constructor_EntersIdleWithoutPolicyChecks()
    {
        // Arrange
        var trace = new List<string>();
        Func<bool> policy = () => { trace.Add("Unexpected policy"); return false; };

        // Act
        var recorder = new Recorder(policy, policy, policy, trace.Add);

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, trace);
    }

    /// <summary>
    /// Covers every accepted pair. Start checks Start and parent-entry permission; Stop checks only
    /// parent-exit permission. Pause and Resume check no policy and run only child hooks. The exact
    /// trace also verifies source mode during exit and target mode during entry.
    /// </summary>
    /// <param name="source">The source leaf mode.</param>
    /// <param name="request">The accepted trigger.</param>
    /// <param name="target">The expected target mode.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle)]
    public void AcceptedPair_ChecksOnlyBoundaryPolicies(RecordingMode source, RecordingRequest request, RecordingMode target)
    {
        // Arrange
        var trace = new List<string>();
        Recorder? recorder = null;
        recorder = new Recorder(
            () => { trace.Add($"CanStart ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"CanEnter ({recorder?.CurrentState})"); return true; },
            () => { trace.Add($"CanExit ({recorder?.CurrentState})"); return true; },
            activity => trace.Add($"{activity} ({recorder?.CurrentState})"));
        Reach(recorder, source);
        trace.Clear();

        // Act
        TransitionResult result = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(target, recorder.CurrentState);
        string[] expected = request switch
        {
            RecordingRequest.Start => ["CanStart (Idle)", "CanEnter (Idle)", "Exit Idle (Idle)",
                "Enter RecordingSession (Recording)", "Enter Recording (Recording)"],
            RecordingRequest.Stop => [$"CanExit ({source})", $"Exit {source} ({source})",
                $"Exit RecordingSession ({source})", "Enter Idle (Idle)"],
            _ => [$"Exit {source} ({source})", $"Enter {target} ({target})"]
        };
        CollectionAssert.AreEqual(expected, trace);
    }

    /// <summary>Undefined pairs preserve the source mode and produce neither permission calls nor lifecycle activity.</summary>
    /// <param name="source">The active leaf mode.</param>
    /// <param name="request">An invalid trigger for that mode.</param>
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
    public void InvalidPair_DoesNotCallPolicies(RecordingMode source, RecordingRequest request)
    {
        // Arrange
        var trace = new List<string>();
        Func<bool> policy = () => { trace.Add("Policy"); return true; };
        var recorder = new Recorder(policy, policy, policy, trace.Add);
        Reach(recorder, source);
        trace.Clear();

        // Act
        TransitionResult result = recorder.TryHandle(request);

        // Assert
        Assert.AreEqual(TransitionResult.InvalidRequest, result);
        Assert.AreEqual(source, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);
    }

    /// <summary>
    /// Denies either Start permission or parent-entry permission. Start stops at the first denial,
    /// leaves Idle active, and runs no lifecycle hooks. Exit permission is irrelevant to Start.
    /// </summary>
    /// <param name="denyStart">Whether the first policy denies rather than the parent-entry policy.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Start_DeniedPolicyPreventsLifecycle(bool denyStart)
    {
        // Arrange
        var trace = new List<string>();
        var recorder = new Recorder(
            () => { trace.Add("CanStart"); return !denyStart; },
            () => { trace.Add("CanEnter"); return false; },
            () => { trace.Add("Unexpected CanExit"); return false; }, trace.Add);
        trace.Clear();

        // Act
        TransitionResult result = recorder.TryHandle(RecordingRequest.Start);

        // Assert
        Assert.AreEqual(TransitionResult.GuardDenied, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(denyStart ? new[] { "CanStart" } : new[] { "CanStart", "CanEnter" }, trace);
    }

    /// <summary>
    /// After Start succeeds, makes all policies return false or throw. Pause and Resume still succeed
    /// with no policy calls and no parent lifecycle events, proving they do not cross the boundary.
    /// </summary>
    /// <param name="throwPolicies">Whether unused policies throw instead of returning false.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ChildChanges_IgnoreDeniedOrThrowingPolicies(bool throwPolicies)
    {
        // Arrange
        bool started = false;
        var trace = new List<string>();
        Func<bool> policy = () =>
        {
            trace.Add("Policy");
            if (!started) return true;
            if (throwPolicies) throw new InvalidOperationException("Policy must not run on child changes.");
            return false;
        };
        var recorder = new Recorder(policy, policy, policy, trace.Add);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        started = true;
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(RecordingMode.Paused, recorder.CurrentState);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));

        // Assert
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Recording", "Enter Paused", "Exit Paused", "Enter Recording" }, trace);
    }

    /// <summary>Denied Stop leaves either child active. Allowing exit on retry exits child then parent and returns Idle.</summary>
    /// <param name="source">The active child when Stop is requested.</param>
    [TestMethod]
    [DataRow(RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused)]
    public void Stop_ReevaluatesExitPermissionOnRetry(RecordingMode source)
    {
        // Arrange
        bool canExit = false;
        var trace = new List<string>();
        var recorder = new Recorder(() => true, () => true,
            () => { trace.Add("CanExit"); return canExit; }, trace.Add);
        Reach(recorder, source);
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Stop));
        Assert.AreEqual(source, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExit" }, trace);

        // Arrange
        canExit = true;
        trace.Clear();

        // Act
        TransitionResult result = recorder.TryHandle(RecordingRequest.Stop);

        // Assert
        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "CanExit", $"Exit {source}", "Exit RecordingSession", "Enter Idle" }, trace);
    }

    /// <summary>Each required boundary policy propagates its original exception before hooks and allows a later retry.</summary>
    /// <param name="policyName">The policy that fails.</param>
    [TestMethod]
    [DataRow("Start")]
    [DataRow("Enter")]
    [DataRow("Exit")]
    public void PolicyException_PropagatesAndProcessingResets(string policyName)
    {
        // Arrange
        bool fail = false;
        var failure = new InvalidOperationException("Boundary policy failed.");
        Func<string, bool> check = name =>
        {
            if (fail && name == policyName) throw failure;
            return true;
        };
        var trace = new List<string>();
        var recorder = new Recorder(() => check("Start"), () => check("Enter"), () => check("Exit"), trace.Add);
        if (policyName == "Exit") Reach(recorder, RecordingMode.Recording);
        RecordingMode source = recorder.CurrentState;
        RecordingRequest request = policyName == "Exit" ? RecordingRequest.Stop : RecordingRequest.Start;
        trace.Clear();
        fail = true;

        // Act & Assert
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(request));
        Assert.AreSame(failure, actual);
        Assert.AreEqual(source, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), trace);

        // Arrange
        fail = false;

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(request));
        Assert.AreEqual(policyName == "Exit" ? RecordingMode.Idle : RecordingMode.Recording, recorder.CurrentState);
    }

    /// <summary>Every callback dependency is validated at construction, including policies used only after lazy initialization.</summary>
    /// <param name="parameter">The omitted callback's parameter name.</param>
    [TestMethod]
    [DataRow("canStartRecording")]
    [DataRow("canEnterSession")]
    [DataRow("canExitSession")]
    [DataRow("reportActivity")]
    public void NullDependency_IsRejected(string parameter)
    {
        // Arrange
        Func<bool>? start = parameter == "canStartRecording" ? null : () => true;
        Func<bool>? enter = parameter == "canEnterSession" ? null : () => true;
        Func<bool>? exit = parameter == "canExitSession" ? null : () => true;
        Action<string>? report = parameter == "reportActivity" ? null : _ => { };

        // Act & Assert
        var failure = Assert.ThrowsExactly<ArgumentNullException>(() => new Recorder(start!, enter!, exit!, report!));
        Assert.AreEqual(parameter, failure.ParamName);
    }

    /// <summary>Nested requests from parent-entry permission or Idle exit are rejected, and later Start remains possible.</summary>
    /// <param name="fromPolicy">Whether the nested request originates in parent-entry permission.</param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NestedRequest_IsRejectedAndLaterStartSucceeds(bool fromPolicy)
    {
        // Arrange
        Recorder? recorder = null;
        bool nested = true;
        var trace = new List<string>();
        recorder = new Recorder(() => true, () =>
        {
            if (nested && fromPolicy) recorder!.TryHandle(RecordingRequest.Start);
            return true;
        }, () => true, activity =>
        {
            trace.Add(activity);
            if (nested && !fromPolicy && activity == "Exit Idle") recorder!.TryHandle(RecordingRequest.Start);
        });
        trace.Clear();

        // Act & Assert
        var failure = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual("A permission or lifecycle callback cannot send a nested request.", failure.Message);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(fromPolicy ? Array.Empty<string>() : new[] { "Exit Idle" }, trace);

        // Arrange
        nested = false;
        trace.Clear();

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter RecordingSession", "Enter Recording" }, trace);
    }

    /// <summary>
    /// Child exit failure retains Recording; child entry failure selects Paused. Neither exception becomes a result.
    /// Reaches Recording, then sends Pause through Recorder.TryHandle, which exercises
    /// RecordingSessionState.ChangeSubstate. If Exit Recording throws, the old child remains
    /// selected as Recording because replacement has not happened. If Enter Paused throws, Paused
    /// has already been selected, so there is no rollback to Recording. In both cases the exception
    /// propagates instead of becoming a transition result, and the trace confirms which hooks ran.
    /// </summary>
    /// <param name="activity">The child lifecycle hook that throws.</param>
    /// <param name="expectedMode">The child still selected after the failure.</param>
    [TestMethod]
    [DataRow("Exit Recording", RecordingMode.Recording)]
    [DataRow("Enter Paused", RecordingMode.Paused)]
    public void ChildLifecycleFailure_PreservesNoRollbackContract(string activity, RecordingMode expectedMode)
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

    /// <summary>Two instances keep their own active children, policies, and lifecycle activity.</summary>
    [TestMethod]
    public void Instances_AreIndependent()
    {
        // Arrange
        var firstTrace = new List<string>();
        var secondTrace = new List<string>();
        var first = new Recorder(() => true, () => true, () => true, firstTrace.Add);
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

    /// <summary>Reaches a source mode using the public API with Start allowed.</summary>
    /// <param name="recorder">The initially Idle recorder.</param>
    /// <param name="mode">The required source mode.</param>
    private static void Reach(Recorder recorder, RecordingMode mode)
    {
        if (mode != RecordingMode.Idle) Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        if (mode == RecordingMode.Paused) Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(mode, recorder.CurrentState);
    }
}
