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

    /// <summary>
    /// Loads a complete, valid transition document and checks that its initial attribute
    /// becomes the table's initial mode, which recorder construction will later use.
    /// </summary>
    [TestMethod]
    public void Load_ValidConfiguration_UsesDeclaredInitialState()
    {
        TransitionTable table = Load(ValidConfiguration);

        Assert.AreEqual(RecordingMode.Idle, table.InitialMode);
    }

    /// <summary>
    /// Constructs a recorder from the shared Idle-initialized configuration.
    /// Verifies both the selected state and exactly one entry callback, with no exit callback.
    /// </summary>
    [TestMethod]
    public void RecorderConstruction_EntersConfiguredInitialState()
    {
        List<string> activity = [];

        Recorder recorder = CreateRecorder(activity);

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activity);
    }

    /// <summary>
    /// Sends User Start, Pause, Resume, and Stop with all permissions allowed.
    /// Checks every result, the final Idle state, and the complete ordered lifecycle trace
    /// so successful transitions cannot omit, duplicate, or reorder entry and exit callbacks.
    /// </summary>
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

    /// <summary>
    /// Sends the catalog-declared User Pause trigger while Idle, where no Pause transition exists.
    /// Expects InvalidRequest and no state or lifecycle changes beyond construction's initial entry.
    /// </summary>
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

    /// <summary>
    /// Supplies a Start guard that records its invocations and denies permission.
    /// A User Start must check that guard exactly once, return GuardDenied, and leave Idle
    /// without running any additional entry or exit callbacks.
    /// </summary>
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

    /// <summary>
    /// Counts Start-permission checks across User Start, Pause, and Resume.
    /// Only Start carries that configured guard, so the count must remain one after returning
    /// to Recording; Pause and Resume must not inherit the Start-only permission check.
    /// </summary>
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

    /// <summary>
    /// Enters Recording with its exit permission denied, then clears the setup lifecycle trace.
    /// Both Pause and Stop must be blocked by the same state-level exit policy, regardless of
    /// their targets, leaving Recording active and producing no new lifecycle activity.
    /// </summary>
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

    /// <summary>
    /// Allows leaving Recording but denies entry to Paused, then sends User Pause.
    /// The recorder must evaluate entry permission before performing lifecycle work: rejection
    /// returns GuardDenied without exiting Recording or entering Paused.
    /// </summary>
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

    /// <summary>
    /// Uses an initial attribute that is not a RecordingMode name.
    /// Loading must fail with FormatException rather than defaulting to a known state.
    /// </summary>
    [TestMethod]
    public void Load_UnknownInitialMode_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"Unknown\"><state name=\"Idle\" /></recorder>"));
    }

    /// <summary>
    /// Declares a state whose name is outside RecordingMode.
    /// The loader must reject the declaration with FormatException during state-name parsing.
    /// </summary>
    [TestMethod]
    public void Load_UnknownStateName_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"Idle\"><state name=\"Unknown\" /></recorder>"));
    }

    /// <summary>
    /// Gives an otherwise well-formed transition an unknown trigger name.
    /// Trigger names must be supported RecordingRequest values, so loading must throw FormatException.
    /// </summary>
    [TestMethod]
    public void Load_UnknownTrigger_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Unknown\" source=\"User\" target=\"Idle\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// Uses a catalog-declared trigger but a target name that is not a RecordingMode.
    /// Expects FormatException for the invalid target rather than accepting an unusable transition.
    /// </summary>
    [TestMethod]
    public void Load_UnknownTarget_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Unknown\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// References Recording, which is a valid enum name but has no state declaration in this document.
    /// Distinguishes target-reference validation from enum parsing: all referenced targets must
    /// belong to the configured state set, otherwise loading throws FormatException.
    /// </summary>
    [TestMethod]
    public void Load_UndeclaredTarget_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// Omits the required target attribute from a transition with a valid trigger identity.
    /// Loading must throw FormatException; an absent target must not imply a default or self-transition.
    /// </summary>
    [TestMethod]
    public void Load_MissingTargetAttribute_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" /></state></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// Defines two targets for the same current state and exact Start/User identity.
    /// This lookup key would be ambiguous, so the loader must throw FormatException rather than
    /// selecting the first or last declaration. Both targets are declared to isolate duplication.
    /// </summary>
    [TestMethod]
    public void Load_DuplicateTransition_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\" /><transition trigger=\"Start\" source=\"User\" target=\"Idle\" /></state><state name=\"Recording\" /></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// Names Idle as the initial mode but declares only Recording.
    /// Even a valid enum value must have a matching state declaration before it can initialize a recorder.
    /// </summary>
    [TestMethod]
    public void Load_InitialModeNotDeclared_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"Idle\"><state name=\"Recording\" /></recorder>"));
    }

    /// <summary>
    /// Attaches an unrecognized guard to an otherwise valid Start transition.
    /// Expects FormatException so a misspelled or unsupported policy cannot be silently ignored.
    /// </summary>
    [TestMethod]
    public void Load_UnsupportedGuard_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\" guard=\"Unknown\" /></state><state name=\"Recording\" /></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// Adds an action child to a transition, although transitions support attributes only.
    /// Expects FormatException rather than ignoring executable-looking configuration the loader does not support.
    /// </summary>
    [TestMethod]
    public void Load_TransitionWithChildElement_ThrowsFormatException()
    {
        const string xml = "<recorder initial=\"Idle\"><state name=\"Idle\"><transition trigger=\"Start\" source=\"User\" target=\"Recording\"><action /></transition></state><state name=\"Recording\" /></recorder>";

        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// Supplies the numeric enum representation of Idle instead of its literal name.
    /// Although enum parsing can accept numbers, configuration requires exact names and must reject this alias.
    /// </summary>
    [TestMethod]
    public void Load_NumericEnumAlias_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("<recorder initial=\"0\"><state name=\"Idle\" /></recorder>"));
    }

    /// <summary>
    /// Passes a null transition stream alongside a valid catalog to isolate the stream argument check.
    /// Expects ArgumentNullException before XML parsing; null-forgiving syntax only permits the intentional test input.
    /// </summary>
    [TestMethod]
    public void Load_NullStream_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TransitionTable.Load(null!, LoadCatalog()));
    }

    /// <summary>
    /// Enters Recording and clears setup activity before dispatching Stop/User.
    /// Checks that the Recording-specific transition succeeds and performs exactly Exit Recording followed by Enter Idle.
    /// </summary>
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

    /// <summary>
    /// Reaches Paused through Start and Pause, then isolates the activity caused by Stop/User.
    /// The same trigger used in Recording must now select Paused's transition and exit Paused before entering Idle.
    /// </summary>
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

    /// <summary>
    /// Dispatches Stop/User immediately after construction, while Idle has no matching transition.
    /// Its availability in other states must not make it valid here: state and construction-only activity stay unchanged.
    /// </summary>
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
