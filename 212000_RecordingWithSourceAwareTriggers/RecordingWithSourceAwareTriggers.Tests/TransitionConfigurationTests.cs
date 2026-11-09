using System.Text;
using RecordingWithSourceAwareTriggers;

namespace RecordingWithSourceAwareTriggers.Tests;

[TestClass]
public sealed class TransitionConfigurationTests
{
    private const string ValidConfiguration = """
        <recorder initial="Idle">
          <state name="Idle">
            <transition trigger="Start" source="User" target="Recording" guard="StartPermission" />
          </state>
          <state name="Recording">
            <transition trigger="Pause" source="User" target="Paused" />
            <transition trigger="Stop" source="User" target="Idle" />
          </state>
          <state name="Paused">
            <transition trigger="Resume" source="User" target="Recording" />
            <transition trigger="Stop" source="User" target="Idle" />
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

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User")));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new RecordingTrigger(RecordingRequest.Pause, "User")));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new RecordingTrigger(RecordingRequest.Resume, "User")));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new RecordingTrigger(RecordingRequest.Stop, "User")));

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

        TransitionResult result = recorder.TryHandle(new RecordingTrigger(RecordingRequest.Pause, "User"));

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

        TransitionResult result = recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User"));

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

        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User"));
        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Pause, "User"));
        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Resume, "User"));

        Assert.AreEqual(1, permissionChecks);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
    }

    [TestMethod]
    public void TryHandle_RecordingExitDenied_BlocksPauseAndStop()
    {
        List<string> activity = [];
        bool canLeaveRecording = false;
        Recorder recorder = CreateRecorder(activity, canLeaveRecording: () => canLeaveRecording);
        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User"));
        activity.Clear();

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(new RecordingTrigger(RecordingRequest.Pause, "User")));
        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(new RecordingTrigger(RecordingRequest.Stop, "User")));

        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        CollectionAssert.AreEqual(Array.Empty<string>(), activity);
    }

    [TestMethod]
    public void TryHandle_PausedEntryDenied_DoesNotExitRecording()
    {
        List<string> activity = [];
        Recorder recorder = CreateRecorder(activity, canEnterPaused: () => false);
        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User"));
        activity.Clear();

        TransitionResult result = recorder.TryHandle(new RecordingTrigger(RecordingRequest.Pause, "User"));

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
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Unknown\" source=\"User\" target=\"Idle\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_UnknownTarget_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Unknown\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_UndeclaredTarget_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_MissingTargetAttribute_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_DuplicateTransition_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\" /><transition trigger=\"Start\" source=\"User\" target=\"Idle\" /></state><state name=\"Recording\" /></recorder>";

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
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\" guard=\"Unknown\" /></state><state name=\"Recording\" /></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    [TestMethod]
    public void Load_TransitionWithChildElement_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\"><action /></transition></state><state name=\"Recording\" /></recorder>";

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
        Assert.ThrowsExactly<ArgumentNullException>(() => TransitionTable.Load(null!, LoadCatalog()));
    }

    [TestMethod]
    public void TryHandle_SourceAwareTrigger_StopFromRecording_GoesToIdle()
    {
        List<string> activity = [];
        Recorder recorder = CreateRecorder(activity);
        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User"));
        activity.Clear();

        TransitionResult result = recorder.TryHandle(new RecordingTrigger(RecordingRequest.Stop, "User"));

        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Recording", "Enter Idle" }, activity);
    }

    [TestMethod]
    public void TryHandle_SourceAwareTrigger_StopFromPaused_GoesToIdle()
    {
        List<string> activity = [];
        Recorder recorder = CreateRecorder(activity);
        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Start, "User"));
        recorder.TryHandle(new RecordingTrigger(RecordingRequest.Pause, "User"));
        activity.Clear();

        TransitionResult result = recorder.TryHandle(new RecordingTrigger(RecordingRequest.Stop, "User"));

        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Exit Paused", "Enter Idle" }, activity);
    }

    [TestMethod]
    public void TryHandle_SourceAwareTrigger_StopNotValidFromIdle()
    {
        List<string> activity = [];
        Recorder recorder = CreateRecorder(activity);

        TransitionResult result = recorder.TryHandle(new RecordingTrigger(RecordingRequest.Stop, "User"));

        Assert.AreEqual(TransitionResult.InvalidRequest, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activity);
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
        return TransitionTable.Load(stream, LoadCatalog());
    }

    private static TriggerCatalog LoadCatalog()
    {
        const string xml = """
            <triggers>
              <trigger name="Start" source="User" />
              <trigger name="Pause" source="User" />
              <trigger name="Pause" source="Device" />
              <trigger name="Resume" source="User" />
              <trigger name="Stop" source="User" />
            </triggers>
            """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml));
        return TriggerCatalog.Load(stream);
    }
}
