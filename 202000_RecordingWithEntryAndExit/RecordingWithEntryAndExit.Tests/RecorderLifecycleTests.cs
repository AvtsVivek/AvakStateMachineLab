using RecordingWithEntryAndExit;

namespace RecordingWithEntryAndExit.Tests;

/// <summary>
/// Verifies transition behavior and lifecycle execution using in-memory traces rather than console capture.
/// </summary>
/// <remarks>
/// The twelve state/request combinations retain the previous exercise's behavior.
/// Additional assertions verify call order, exact call counts, and the mode visible during callbacks.
/// </remarks>
[TestClass]
public sealed class RecorderLifecycleTests
{
    /// <summary>Verifies construction enters Idle exactly once without first exiting a state.</summary>
    [TestMethod]
    public void NewRecorder_EntersIdleOnce()
    {
        List<string> activities = [];

        Recorder recorder = new(activities.Add);

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activities);
    }

    /// <summary>Verifies each accepted transition exits the source before entering the destination.</summary>
    /// <param name="source">The starting mode established using public requests.</param>
    /// <param name="request">The trigger for an allowed transition.</param>
    /// <param name="target">The expected destination mode.</param>
    /// <remarks>
    /// Each callback also reads CurrentState. An exit must observe the source and an entry must
    /// observe the destination, proving state replacement occurs between the two actions.
    /// </remarks>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle)]
    public void AllowedRequest_ExitsSourceThenEntersTarget(
        RecordingMode source, RecordingRequest request, RecordingMode target)
    {
        List<(string Activity, RecordingMode? Mode)> observations = [];
        Recorder? recorder = null;
        recorder = new(activity => observations.Add((activity, recorder?.CurrentState)));
        MoveTo(recorder, source);
        // Construction invokes the callback before assignment completes; only this transition is under test.
        observations.Clear();

        bool accepted = recorder.TryHandle(request);

        Assert.IsTrue(accepted);
        Assert.AreEqual(target, recorder.CurrentState);
        (string Activity, RecordingMode? Mode)[] expected =
        [
            ($"Exit {source}", source),
            ($"Enter {target}", target)
        ];
        CollectionAssert.AreEqual(expected, observations);
    }

    /// <summary>Verifies every disallowed pair leaves the mode unchanged and executes no lifecycle actions.</summary>
    /// <param name="source">The starting mode.</param>
    /// <param name="request">A trigger with no transition from the starting mode.</param>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Start)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Start)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Pause)]
    public void DisallowedRequest_RunsNoLifecycleActions(RecordingMode source, RecordingRequest request)
    {
        List<string> activities = [];
        Recorder recorder = new(activities.Add);
        MoveTo(recorder, source);
        activities.Clear();

        bool accepted = recorder.TryHandle(request);

        Assert.IsFalse(accepted);
        Assert.AreEqual(source, recorder.CurrentState);
        Assert.IsEmpty(activities);
    }

    /// <summary>Verifies each revisit performs entry again and the complete lifecycle trace stays ordered.</summary>
    [TestMethod]
    public void StartPauseResumeStop_RecordsExactLifecycleSequence()
    {
        List<string> activities = [];
        Recorder recorder = new(activities.Add);

        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Pause));
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Resume));
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Stop));

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(
            new[]
            {
                "Enter Idle",
                "Exit Idle", "Enter Recording",
                "Exit Recording", "Enter Paused",
                "Exit Paused", "Enter Recording",
                "Exit Recording", "Enter Idle"
            },
            activities);
    }

    /// <summary>Verifies a request on one recorder changes neither the other recorder nor its activity log.</summary>
    [TestMethod]
    public void RecorderInstances_KeepStatesAndActivityIndependent()
    {
        List<string> firstActivities = [];
        List<string> secondActivities = [];
        Recorder first = new(firstActivities.Add);
        Recorder second = new(secondActivities.Add);
        firstActivities.Clear();
        secondActivities.Clear();

        bool accepted = first.TryHandle(RecordingRequest.Start);

        Assert.IsTrue(accepted);
        Assert.AreEqual(RecordingMode.Recording, first.CurrentState);
        Assert.AreEqual(RecordingMode.Idle, second.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter Recording" }, firstActivities);
        Assert.IsEmpty(secondActivities);
    }

    /// <summary>Verifies a missing activity callback is rejected explicitly rather than silently ignored.</summary>
    [TestMethod]
    public void NullActivityCallback_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new Recorder(null!));

        Assert.AreEqual("reportActivity", exception.ParamName);
    }

    /// <summary>Verifies callback exceptions propagate and the active mode reflects where execution stopped.</summary>
    /// <param name="failingActivity">The lifecycle action that throws.</param>
    /// <param name="expectedMode">The active mode after that failure, without rollback.</param>
    [TestMethod]
    [DataRow("Exit Recording", RecordingMode.Recording)]
    [DataRow("Enter Paused", RecordingMode.Paused)]
    public void LifecycleCallbackFailure_PropagatesWithoutRollback(string failingActivity, RecordingMode expectedMode)
    {
        List<string> activities = [];
        InvalidOperationException failure = new("Activity observer failed.");
        Recorder recorder = new(activity =>
        {
            activities.Add(activity);
            if (activity == failingActivity)
            {
                throw failure;
            }
        });
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        activities.Clear();

        InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(
            () => recorder.TryHandle(RecordingRequest.Pause));

        Assert.AreSame(failure, actual);
        Assert.AreEqual(expectedMode, recorder.CurrentState);
        string[] expected = failingActivity == "Exit Recording"
            ? ["Exit Recording"]
            : ["Exit Recording", "Enter Paused"];
        CollectionAssert.AreEqual(expected, activities);
    }

    /// <summary>Verifies nested requests fail explicitly and do not interleave lifecycle execution.</summary>
    [TestMethod]
    public void NestedRequestFromCallback_ThrowsAndAllowsLaterRequest()
    {
        List<string> activities = [];
        Recorder? recorder = null;
        bool sendNestedRequest = true;
        recorder = new(activity =>
        {
            activities.Add(activity);
            if (activity == "Exit Idle" && sendNestedRequest)
            {
                Assert.IsNotNull(recorder);
                recorder.TryHandle(RecordingRequest.Start);
            }
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle", "Exit Idle" }, activities);
        sendNestedRequest = false;
        activities.Clear();
        Assert.IsTrue(recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Idle", "Enter Recording" }, activities);
    }

    /// <summary>Establishes a source mode through accepted public requests without assigning state directly.</summary>
    /// <param name="recorder">The fresh recorder to prepare.</param>
    /// <param name="mode">The defined mode required by a data row.</param>
    private static void MoveTo(Recorder recorder, RecordingMode mode)
    {
        if (mode is RecordingMode.Recording or RecordingMode.Paused)
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
