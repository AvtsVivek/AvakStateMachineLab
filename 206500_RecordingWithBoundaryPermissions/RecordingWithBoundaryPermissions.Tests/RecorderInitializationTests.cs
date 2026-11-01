using System.Reflection;

namespace RecordingWithBoundaryPermissions.Tests;

/// <summary>Verifies allocation separately from entry activity, so the lazy-creation requirement is checked directly.</summary>
[TestClass]
public sealed class RecorderInitializationTests
{
    /// <summary>
    /// Idle observation, invalid requests, policy denial, and a throwing entry policy do not allocate
    /// the composite. A permitted Start creates it; Stop deactivates it, and subsequent Start reuses
    /// the same object while checking both entry policies again and repeating lifecycle entry.
    /// </summary>
    [TestMethod]
    public void Session_IsAllocatedAfterPermissionAndReusedOnRestart()
    {
        // Arrange
        var field = typeof(Recorder).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Recorder session field was not found.");
        bool canStart = false;
        bool canEnter = false;
        bool throwEntry = false;
        var failure = new InvalidOperationException("Entry policy failed.");
        var trace = new List<string>();
        var recorder = new Recorder(() => canStart, () =>
        {
            if (throwEntry) throw failure;
            return canEnter;
        }, () => true, trace.Add);

        // Act & Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.IsNull(field.GetValue(recorder));
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.IsNull(field.GetValue(recorder));

        // Arrange
        canStart = true;

        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.IsNull(field.GetValue(recorder));

        // Arrange
        throwEntry = true;

        // Act & Assert
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start)));
        Assert.IsNull(field.GetValue(recorder));
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, trace);

        // Arrange
        throwEntry = false;
        canEnter = true;

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        object? session = field.GetValue(recorder);
        Assert.IsNotNull(session);
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Stop));
        Assert.AreSame(session, field.GetValue(recorder));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);

        // Arrange
        canEnter = false;

        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreSame(session, field.GetValue(recorder));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);

        // Arrange
        canEnter = true;

        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreSame(session, field.GetValue(recorder));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter RecordingSession", "Enter Recording",
            "Exit Recording", "Exit RecordingSession", "Enter Idle",
            "Exit Idle", "Enter RecordingSession", "Enter Recording"
        }, trace);
    }
}
