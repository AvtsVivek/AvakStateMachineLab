using System.Text;
using RecordingWithXmlTransitions;

namespace RecordingWithXmlTransitions.Tests;

[TestClass]
public sealed class TransitionConfigurationTests
{
    private const string ValidConfiguration = """
        <recorder initial="Idle">
          <state name="Idle">
            <transition trigger="Start" target="Recording" guard="StartPermission" />
          </state>
          <state name="Recording">
            <transition trigger="Pause" target="Paused" />
            <transition trigger="Stop" target="Idle" />
          </state>
          <state name="Paused">
            <transition trigger="Resume" target="Recording" />
            <transition trigger="Stop" target="Idle" />
          </state>
        </recorder>
        """;

    [TestMethod]
    public void Load_ValidConfiguration_UsesDeclaredInitialState()
    {
        TransitionTable table = Load(ValidConfiguration);

        Assert.AreEqual(RecordingMode.Idle, table.InitialMode);
    }

    [TestMethod]
    public void RecorderConstruction_EntersConfiguredInitialState()
    {
        List<string> activity = [];

        Recorder recorder = CreateRecorder(activity);

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activity);
    }

    [TestMethod]
    public void TryHandle_ValidSequence_FollowsXmlTransitionsAndLifecycle()
    {
        List<string> activity = [];
        Recorder recorder = CreateRecorder(activity);

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Start));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Resume));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(RecordingRequest.Stop));

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter Recording", "Exit Recording", "Enter Paused",
            "Exit Paused", "Enter Recording", "Exit Recording", "Enter Idle"
        }, activity);
    }

    [TestMethod]
    public void TryHandle_UndefinedTransition_ReturnsInvalidWithoutLifecycleWork()
    {
        List<string> activity = [];
        Recorder recorder = CreateRecorder(activity);

        TransitionResult result = recorder.TryHandle(RecordingRequest.Pause);

        Assert.AreEqual(TransitionResult.InvalidRequest, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activity);
    }

    [TestMethod]
    public void TryHandle_StartPermissionDenied_DoesNotLeaveIdle()
    {
        List<string> activity = [];
        int permissionChecks = 0;
        Recorder recorder = CreateRecorder(activity, canStartRecording: () => { permissionChecks++; return false; });

        TransitionResult result = recorder.TryHandle(RecordingRequest.Start);

        Assert.AreEqual(TransitionResult.GuardDenied, result);
        Assert.AreEqual(1, permissionChecks);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activity);
    }

    [TestMethod]
    public void TryHandle_StartPermission_IsNotUsedForOtherTransitions()
    {
        List<string> activity = [];
        int permissionChecks = 0;
        Recorder recorder = CreateRecorder(activity, canStartRecording: () => { permissionChecks++; return true; });

        recorder.TryHandle(RecordingRequest.Start);
        recorder.TryHandle(RecordingRequest.Pause);
        recorder.TryHandle(RecordingRequest.Resume);

        Assert.AreEqual(1, permissionChecks);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
    }

    [TestMethod]
    public void TryHandle_RecordingExitDenied_BlocksPauseAndStop()
    {
        List<string> activity = [];
        bool canLeaveRecording = false;
        Recorder recorder = CreateRecorder(activity, canLeaveRecording: () => canLeaveRecording);
        recorder.TryHandle(RecordingRequest.Start);
        activity.Clear();

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Pause));
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(RecordingRequest.Stop));

        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), activity);
    }

    [TestMethod]
    public void TryHandle_PausedEntryDenied_DoesNotExitRecording()
    {
        List<string> activity = [];
        Recorder recorder = CreateRecorder(activity, canEnterPaused: () => false);
        recorder.TryHandle(RecordingRequest.Start);
        activity.Clear();

        TransitionResult result = recorder.TryHandle(RecordingRequest.Pause);

        Assert.AreEqual(TransitionResult.GuardDenied, result);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), activity);
    }

    [TestMethod]
    public void Load_UnknownInitialMode_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"Unknown\"><state name=\"Idle\" /></recorder>"));
    }

    [TestMethod]
    public void Load_UnknownStateName_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"Idle\"><state name=\"Unknown\" /></recorder>"));
    }

    [TestMethod]
    public void Load_UnknownTrigger_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Unknown\" target=\"Idle\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_UnknownTarget_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" target=\"Unknown\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_UndeclaredTarget_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" target=\"Recording\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_MissingTargetAttribute_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_DuplicateTransition_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" target=\"Recording\" /><transition trigger=\"Start\" target=\"Idle\" /></state><state name=\"Recording\" /></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_InitialModeNotDeclared_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"Idle\"><state name=\"Recording\" /></recorder>"));
    }

    [TestMethod]
    public void Load_UnsupportedGuard_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" target=\"Recording\" guard=\"Unknown\" /></state><state name=\"Recording\" /></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_TransitionWithChildElement_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" target=\"Recording\"><action /></transition></state><state name=\"Recording\" /></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_NumericEnumAlias_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"0\"><state name=\"Idle\" /></recorder>"));
    }

    [TestMethod]
    public void Load_NullStream_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TransitionTable.Load(null!));
    }

    private static Recorder CreateRecorder(List<string> activity,
        Func<bool>? canStartRecording = null,
        Func<bool>? canLeaveRecording = null,
        Func<bool>? canEnterPaused = null)
    {
        return new Recorder(Load(ValidConfiguration),
            canStartRecording ?? (() => true),
            canLeaveRecording ?? (() => true),
            canEnterPaused ?? (() => true),
            activity.Add);
    }

    private static TransitionTable Load(string xml)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
        return TransitionTable.Load(stream);
    }
}
