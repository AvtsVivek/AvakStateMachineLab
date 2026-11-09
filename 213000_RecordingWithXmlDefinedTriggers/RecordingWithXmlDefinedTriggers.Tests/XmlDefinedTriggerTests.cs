using System.Text;
using System.Xml;
using RecordingWithXmlDefinedTriggers;

namespace RecordingWithXmlDefinedTriggers.Tests;

[TestClass]
public sealed class XmlDefinedTriggerTests
{
    /// <summary>
    /// Loads the shipped XML and exercises the full user-driven lifecycle with its custom names.
    /// Asserts each dispatch succeeds, returns to Idle, and produces the exact ordered lifecycle trace.
    /// </summary>
    [TestMethod]
    public void ShippedConfiguration_UsesXmlDefinedNamesForLifecycle()
    {
        TransitionTable table = LoadShippedConfiguration();
        List<string> activity = [];
        bool canStart = true;
        Recorder recorder = new(table, () => canStart, () => true, () => true, activity.Add);

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("StartCapture", "User")));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("PauseCapture", "User")));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("ResumeCapture", "User")));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("StopCapture", "User")));

        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[]
        {
            "Enter Idle", "Exit Idle", "Enter Recording", "Exit Recording", "Enter Paused",
            "Exit Paused", "Enter Recording", "Exit Recording", "Enter Idle"
        }, activity);
    }

    /// <summary>
    /// Defines BeginCaptureNow only in XML, then dispatches that exact identity.
    /// Verifies an arbitrary catalog name works despite having no corresponding enum member.
    /// </summary>
    [TestMethod]
    public void CustomNameNotRepresentedByEnum_IsAcceptedAndDispatched()
    {
        using MemoryStream stream = XmlStream("""
            <recorder initial="Idle">
              <triggers><trigger name="BeginCaptureNow" source="Console" /></triggers>
              <state name="Idle"><transition trigger="BeginCaptureNow" source="Console" target="Recording" /></state>
              <state name="Recording" />
            </recorder>
            """);
        Recorder recorder = new(TransitionTable.Load(stream), () => true, () => true, () => true, _ => { });

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("BeginCaptureNow", "Console")));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
    }

    /// <summary>
    /// Starts two recorders in the same state and sends PauseCapture from different sources.
    /// Verifies that User enters Paused while Device follows its own configured path to Idle.
    /// </summary>
    [TestMethod]
    public void SameNameFromDifferentSources_SelectsDifferentTransitions()
    {
        TransitionTable table = LoadShippedConfiguration();
        Recorder userRecorder = new(table, () => true, () => true, () => true, _ => { });
        Recorder deviceRecorder = new(table, () => true, () => true, () => true, _ => { });

        Assert.AreEqual(TransitionResult.Transitioned, userRecorder.TryHandle(new("StartCapture", "User")));
        Assert.AreEqual(TransitionResult.Transitioned, deviceRecorder.TryHandle(new("StartCapture", "User")));
        Assert.AreEqual(TransitionResult.Transitioned, userRecorder.TryHandle(new("PauseCapture", "User")));
        Assert.AreEqual(RecordingMode.Paused, userRecorder.CurrentState);
        Assert.AreEqual(TransitionResult.Transitioned, deviceRecorder.TryHandle(new("PauseCapture", "Device")));
        Assert.AreEqual(RecordingMode.Idle, deviceRecorder.CurrentState);
    }

    /// <summary>
    /// Enters Recording, then varies only the case or declared identity of a trigger name/source.
    /// Each mismatch must return InvalidRequest without changing the recorder state.
    /// </summary>
    [TestMethod]
    public void UnknownNameOrSource_DoesNotFallback()
    {
        Recorder recorder = CreateRecorder();
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("StartCapture", "User")));

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new("PauseCapture", "device")));
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new("pausecapture", "User")));
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new("UnlistedTrigger", "User")));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
    }

    /// <summary>
    /// Uses a catalog-declared Device trigger in Idle, where no matching transition exists.
    /// Confirms catalog membership alone does not permit a transition in every state.
    /// </summary>
    [TestMethod]
    public void DeclaredNameWithoutTransition_IsInvalidInCurrentState()
    {
        Recorder recorder = CreateRecorder();

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new("PauseCapture", "Device")));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
    }

    /// <summary>
    /// Sends an empty name and a whitespace-only source as invalid runtime inputs.
    /// Both must throw ArgumentException before transition lookup.
    /// </summary>
    [TestMethod]
    public void EmptyNameOrSource_IsRejected()
    {
        Recorder recorder = CreateRecorder();

        Assert.ThrowsExactly<ArgumentException>(() => recorder.TryHandle(new("", "User")));
        Assert.ThrowsExactly<ArgumentException>(() => recorder.TryHandle(new("StartCapture", " ")));
    }

    /// <summary>
    /// Loads the shipped StartCapture transition with its guard denied.
    /// Verifies GuardDenied leaves the recorder in Idle and performs no transition lifecycle work.
    /// </summary>
    [TestMethod]
    public void StartPermissionGuard_DeniesBeforeLifecycleWork()
    {
        List<string> activity = [];
        Recorder recorder = new(LoadShippedConfiguration(), () => false, () => true, () => true, activity.Add);

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(new("StartCapture", "User")));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activity);
    }

    /// <summary>
    /// Declares a state outside RecordingMode while keeping the configuration root valid.
    /// The loader must reject the unknown state name with FormatException.
    /// </summary>
    [TestMethod]
    public void Load_UnknownStateName_IsRejected()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("""
            <recorder initial="Idle">
              <triggers><trigger name="Begin" source="User" /></triggers>
              <state name="Unknown" />
            </recorder>
            """));
    }

    /// <summary>
    /// Declares Begin/User but references Begin/Console from a transition.
    /// The full identity must exist in the catalog; matching the name alone is insufficient.
    /// </summary>
    [TestMethod]
    public void Load_UnknownTriggerIdentity_IsRejected()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("""
            <recorder initial="Idle">
              <triggers><trigger name="Begin" source="User" /></triggers>
              <state name="Idle"><transition trigger="Begin" source="Console" target="Recording" /></state>
              <state name="Recording" />
            </recorder>
            """));
    }

    /// <summary>
    /// Repeats the exact Begin/User identity in the catalog.
    /// Duplicate pairs must fail rather than being silently coalesced.
    /// </summary>
    [TestMethod]
    public void Load_DuplicateNameWithSameSource_IsRejected()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("""
            <recorder initial="Idle">
              <triggers><trigger name="Begin" source="User" /><trigger name="Begin" source="User" /></triggers>
              <state name="Idle" />
            </recorder>
            """));
    }

    /// <summary>
    /// Validates empty catalogs, missing or blank fields, unsupported attributes, and invalid child elements.
    /// Each row is well-formed XML that violates the catalog contract and must throw FormatException.
    /// </summary>
    [TestMethod]
    [DataRow("<triggers />")]
    [DataRow("<triggers><trigger source=\"User\" /></triggers>")]
    [DataRow("<triggers><trigger name=\"Begin\" /></triggers>")]
    [DataRow("<triggers><trigger name=\" \" source=\"User\" /></triggers>")]
    [DataRow("<triggers><trigger name=\"Begin\" source=\"User\" extra=\"x\" /></triggers>")]
    [DataRow("<triggers><other /></triggers>")]
    public void Load_InvalidCatalogContent_IsRejected(string triggers)
    {
        string xml = $"<recorder initial=\"Idle\">{triggers}<state name=\"Idle\" /></recorder>";
        Assert.ThrowsExactly<FormatException>(() => Load(xml));
    }

    /// <summary>
    /// Supplies a DTD with an internal entity in the trigger name.
    /// The secure XML reader must reject the declaration with XmlException.
    /// </summary>
    [TestMethod]
    public void Load_Dtd_IsProhibited()
    {
        using MemoryStream stream = XmlStream("""
            <!DOCTYPE recorder [<!ENTITY triggerName "Begin">]>
            <recorder initial="Idle">
              <triggers><trigger name="&triggerName;" source="User" /></triggers>
              <state name="Idle" />
            </recorder>
            """);
        Assert.ThrowsExactly<XmlException>(() => TransitionTable.Load(stream));
    }

    /// <summary>
    /// Passes a null input stream directly to the public loader.
    /// The API must reject the argument with ArgumentNullException before reading XML.
    /// </summary>
    [TestMethod]
    public void Load_NullStream_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TransitionTable.Load(null!));
    }

    private static Recorder CreateRecorder() => new(LoadShippedConfiguration(), () => true, () => true, () => true, _ => { });

    private static TransitionTable LoadShippedConfiguration()
    {
        using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "recorder-transitions.xml"));
        return TransitionTable.Load(stream);
    }

    private static TransitionTable Load(string xml)
    {
        using MemoryStream stream = XmlStream(xml);
        return TransitionTable.Load(stream);
    }

    private static MemoryStream XmlStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
