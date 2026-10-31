using System.Reflection;

namespace RecordingWithHierarchy.Tests;

/// <summary>Checks session allocation timing separately from observable lifecycle entry.</summary>
[TestClass]
public sealed class RecorderInitializationTests
{
    /// <summary>Allocates only after both Start policies pass and reuses the inactive composite after Stop.</summary>
    /// <remarks>Inspects the private reference because lifecycle traces cannot prove whether an object was allocated.</remarks>
    [TestMethod]
    public void Session_IsCreatedOnlyForPermittedStartAndReusedAfterStop()
    {
        // Arrange
        var field = typeof(Recorder).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Recorder session field was not found.");
        bool canStart = false;
        bool canEnter = false;
        bool throwOnEnter = false;
        var failure = new InvalidOperationException("Entry permission failed.");
        var activities = new List<string>();
        var recorder = new Recorder(() => canStart, () =>
        {
            if (throwOnEnter)
            {
                throw failure;
            }
            return canEnter;
        }, () => true, activities.Add);

        // Assert
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.IsNull(field.GetValue(recorder));
        // Act & Assert
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(RecordingRequest.Pause));
        Assert.IsNull(field.GetValue(recorder));
        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.IsNull(field.GetValue(recorder));

        canStart = true;
        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.IsNull(field.GetValue(recorder));
        throwOnEnter = true;
        // Act & Assert
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => recorder.TryHandle(RecordingRequest.Start));
        // Assert
        Assert.AreSame(failure, actual);
        Assert.IsNull(field.GetValue(recorder));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activities);

        throwOnEnter = false;
        canEnter = true;
        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        object? session = field.GetValue(recorder);
        Assert.IsNotNull(session);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Stop));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreSame(session, field.GetValue(recorder));

        canEnter = false;
        // Act & Assert
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreSame(session, field.GetValue(recorder));
        canEnter = true;
        // Act & Assert
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        // Assert
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        Assert.AreSame(session, field.GetValue(recorder));
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter RecordingSession", "Enter Recording",
            "Exit Recording", "Exit RecordingSession", "Enter Idle",
            "Exit Idle", "Enter RecordingSession", "Enter Recording"
        }, activities);
    }
}
