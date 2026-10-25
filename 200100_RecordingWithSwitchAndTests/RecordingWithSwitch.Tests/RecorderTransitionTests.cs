using RecordingWithSwitch;

namespace RecordingWithSwitch.Tests;

/// <summary>
/// Verifies the observable behavior of the switch-based <see cref="Recorder"/>:
/// its initial state, accepted transitions, rejected requests, and instance independence.
/// </summary>
/// <remarks>
/// The data-driven tests cover all twelve combinations of the three defined states
/// and four defined requests. Each test uses a new recorder and exercises its public API
/// without assigning its state directly or depending on another test's execution.
/// </remarks>
[TestClass]
public sealed class RecorderTransitionTests
{
    /// <summary>
    /// Verifies that a newly constructed recorder starts in <see cref="RecordingState.Idle"/>
    /// before receiving any requests.
    /// </summary>
    [TestMethod]
    public void NewRecorder_StartsIdle()
    {
        Recorder recorder = new();

        Assert.AreEqual(RecordingState.Idle, recorder.CurrentState);
    }

    /// <summary>
    /// Verifies that a defined transition accepts its request and reaches the expected target state.
    /// </summary>
    /// <param name="initialState">The source state reached through valid setup requests.</param>
    /// <param name="request">The request acting as the trigger for the transition under test.</param>
    /// <param name="expectedState">The target state required by the transition rules.</param>
    /// <remarks>
    /// Each data row runs as a separate test case. The five rows represent every allowed
    /// transition, and both the acceptance result and resulting state are checked.
    /// </remarks>
    [TestMethod]
    [DataRow(RecordingState.Idle, RecordingRequest.Start, RecordingState.Recording)]
    [DataRow(RecordingState.Recording, RecordingRequest.Pause, RecordingState.Paused)]
    [DataRow(RecordingState.Recording, RecordingRequest.Stop, RecordingState.Idle)]
    [DataRow(RecordingState.Paused, RecordingRequest.Resume, RecordingState.Recording)]
    [DataRow(RecordingState.Paused, RecordingRequest.Stop, RecordingState.Idle)]
    public void AllowedRequest_ChangesToExpectedState(
        RecordingState initialState,
        RecordingRequest request,
        RecordingState expectedState)
    {
        Recorder recorder = CreateRecorderInState(initialState);

        bool accepted = recorder.TryHandle(request);

        // Acceptance and destination are separate guarantees; verify both.
        Assert.IsTrue(accepted, $"{request} should be accepted from {initialState}.");
        Assert.AreEqual(expectedState, recorder.CurrentState);
    }

    /// <summary>
    /// Verifies that a request with no defined transition is rejected without changing the state.
    /// </summary>
    /// <param name="initialState">The state in which the request must be rejected.</param>
    /// <param name="request">A defined request that is not allowed from the source state.</param>
    /// <remarks>
    /// The seven data rows complement the five allowed transitions to cover the full
    /// state/request matrix. Rejection must return false and preserve the source state;
    /// checking only the return value would not establish that no state change occurred.
    /// </remarks>
    [TestMethod]
    [DataRow(RecordingState.Idle, RecordingRequest.Pause)]
    [DataRow(RecordingState.Idle, RecordingRequest.Resume)]
    [DataRow(RecordingState.Idle, RecordingRequest.Stop)]
    [DataRow(RecordingState.Recording, RecordingRequest.Start)]
    [DataRow(RecordingState.Recording, RecordingRequest.Resume)]
    [DataRow(RecordingState.Paused, RecordingRequest.Start)]
    [DataRow(RecordingState.Paused, RecordingRequest.Pause)]
    public void DisallowedRequest_IsRejectedAndLeavesStateUnchanged(
        RecordingState initialState,
        RecordingRequest request)
    {
        Recorder recorder = CreateRecorderInState(initialState);

        bool accepted = recorder.TryHandle(request);

        // Reporting rejection is not enough: it must also leave the state unchanged.
        Assert.IsFalse(accepted, $"{request} should be rejected from {initialState}.");
        Assert.AreEqual(initialState, recorder.CurrentState);
    }

    /// <summary>
    /// Verifies that starting one recorder does not change the state of a second recorder.
    /// </summary>
    /// <remarks>
    /// Both instances start idle. After a successful Start request on the first instance,
    /// the first must be recording while the second remains idle. This checks that the
    /// current state belongs to an instance rather than being shared globally.
    /// </remarks>
    [TestMethod]
    public void RecorderInstances_MaintainIndependentStates()
    {
        Recorder first = new();
        Recorder second = new();

        bool accepted = first.TryHandle(RecordingRequest.Start);

        // Starting one recorder must not change another recorder's state.
        Assert.IsTrue(accepted);
        Assert.AreEqual(RecordingState.Recording, first.CurrentState);
        Assert.AreEqual(RecordingState.Idle, second.CurrentState);
    }

    /// <summary>
    /// Creates a fresh recorder and reaches a defined source state through its public request API.
    /// </summary>
    /// <param name="state">
    /// The defined state required by the calling test: Idle, Recording, or Paused.
    /// </param>
    /// <returns>A new recorder prepared for the calling test's transition or rejection check.</returns>
    /// <remarks>
    /// Idle needs no setup requests. Recording requires Start, and Paused requires Start
    /// followed by Pause. Assertions check that these prerequisite requests are accepted,
    /// so a setup failure is reported before the request under test is executed.
    /// This helper is used only with the defined states supplied by the test data.
    /// </remarks>
    private static Recorder CreateRecorderInState(RecordingState state)
    {
        Recorder recorder = new();

        if (state is RecordingState.Recording or RecordingState.Paused)
        {
            bool started = recorder.TryHandle(RecordingRequest.Start);
            Assert.IsTrue(started, "The recorder setup should be able to start recording.");
        }

        if (state == RecordingState.Paused)
        {
            bool paused = recorder.TryHandle(RecordingRequest.Pause);
            Assert.IsTrue(paused, "The recorder setup should be able to pause recording.");
        }

        return recorder;
    }
}
