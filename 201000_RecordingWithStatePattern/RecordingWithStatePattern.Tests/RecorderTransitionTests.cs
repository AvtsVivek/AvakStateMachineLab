using RecordingWithStatePattern;

namespace RecordingWithStatePattern.Tests;

/// <summary>
/// Verifies the observable behavior of the recorder whose request handling is delegated to state objects.
/// </summary>
/// <remarks>
/// These tests use the same state/request matrix as the switch-based exercise so both implementations
/// can be compared against identical behavior. Tests interact with the public recorder API only.
/// </remarks>
[TestClass]
public sealed class RecorderTransitionTests
{
    /// <summary>
    /// Verifies that a newly constructed recorder starts in the idle mode.
    /// </summary>
    [TestMethod]
    public void NewRecorder_StartsIdle()
    {
        Recorder recorder = new();

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
    }

    /// <summary>
    /// Verifies that each allowed request is accepted and changes the recorder to its defined target mode.
    /// </summary>
    /// <param name="initialState">The source mode reached through valid setup requests.</param>
    /// <param name="request">The request acting as the trigger for the transition under test.</param>
    /// <param name="expectedState">The target mode required by the transition rules.</param>
    /// <remarks>
    /// Each data row runs independently. The rows cover all five allowed transitions;
    /// both the acceptance result and resulting mode are verified.
    /// </remarks>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle)]
    public void AllowedRequest_ChangesToExpectedState(
        RecordingMode initialState,
        RecordingRequest request,
        RecordingMode expectedState)
    {
        Recorder recorder = CreateRecorderInState(initialState);

        bool accepted = recorder.TryHandle(request);

        Assert.IsTrue(accepted, $"{request} should be accepted from {initialState}.");
        Assert.AreEqual(expectedState, recorder.CurrentState);
    }

    /// <summary>
    /// Verifies that each request without a defined transition is rejected and leaves the current mode unchanged.
    /// </summary>
    /// <param name="initialState">The mode in which the request must be rejected.</param>
    /// <param name="request">A defined request that is not allowed from the source mode.</param>
    /// <remarks>
    /// These seven rows complement the five allowed transitions to cover all twelve combinations
    /// of the three modes and four requests. The result and unchanged mode are separate guarantees.
    /// </remarks>
    [TestMethod]
    [DataRow(RecordingMode.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Start)]
    [DataRow(RecordingMode.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Start)]
    [DataRow(RecordingMode.Paused, RecordingRequest.Pause)]
    public void DisallowedRequest_IsRejectedAndLeavesStateUnchanged(
        RecordingMode initialState,
        RecordingRequest request)
    {
        Recorder recorder = CreateRecorderInState(initialState);

        bool accepted = recorder.TryHandle(request);

        Assert.IsFalse(accepted, $"{request} should be rejected from {initialState}.");
        Assert.AreEqual(initialState, recorder.CurrentState);
    }

    /// <summary>
    /// Verifies that a state transition in one recorder does not affect another recorder instance.
    /// </summary>
    [TestMethod]
    public void RecorderInstances_MaintainIndependentStates()
    {
        Recorder first = new();
        Recorder second = new();

        bool accepted = first.TryHandle(RecordingRequest.Start);

        Assert.IsTrue(accepted);
        Assert.AreEqual(RecordingMode.Recording, first.CurrentState);
        Assert.AreEqual(RecordingMode.Idle, second.CurrentState);
    }

    /// <summary>
    /// Creates a new recorder and reaches the requested source mode through accepted public requests.
    /// </summary>
    /// <param name="mode">The defined source mode required by the calling test.</param>
    /// <returns>A recorder prepared for the transition or rejection check.</returns>
    /// <remarks>
    /// Recording is reached by sending Start. Paused is reached by sending Start followed by Pause.
    /// Assertions make setup failures explicit and avoid directly assigning the recorder's state.
    /// </remarks>
    private static Recorder CreateRecorderInState(RecordingMode mode)
    {
        Recorder recorder = new();

        if (mode is RecordingMode.Recording or RecordingMode.Paused)
        {
            bool started = recorder.TryHandle(RecordingRequest.Start);
            Assert.IsTrue(started, "The recorder setup should be able to start recording.");
        }

        if (mode == RecordingMode.Paused)
        {
            bool paused = recorder.TryHandle(RecordingRequest.Pause);
            Assert.IsTrue(paused, "The recorder setup should be able to pause recording.");
        }

        return recorder;
    }
}
