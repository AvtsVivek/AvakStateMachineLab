using System.Text;
using System.Xml;
using RecordingWithXmlDefinedTriggers;

namespace RecordingWithXmlDefinedTriggers.Tests;

[TestClass]
public sealed class XmlDefinedTriggerTests
{
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

    [TestMethod]
    public void DeclaredNameWithoutTransition_IsInvalidInCurrentState()
    {
        Recorder recorder = CreateRecorder();

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new("PauseCapture", "Device")));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
    }

    [TestMethod]
    public void EmptyNameOrSource_IsRejected()
    {
        Recorder recorder = CreateRecorder();

        Assert.ThrowsExactly<ArgumentException>(() => recorder.TryHandle(new("", "User")));
        Assert.ThrowsExactly<ArgumentException>(() => recorder.TryHandle(new("StartCapture", " ")));
    }

    [TestMethod]
    public void StartPermissionGuard_DeniesBeforeLifecycleWork()
    {
        List<string> activity = [];
        Recorder recorder = new(LoadShippedConfiguration(), () => false, () => true, () => true, activity.Add);

        Assert.AreEqual(TransitionResult.GuardDenied, recorder.TryHandle(new("StartCapture", "User")));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        CollectionAssert.AreEqual(new[] { "Enter Idle" }, activity);
    }

    [TestMethod]
    public void Load_UnknownStateName_IsRejected()
    {
        Assert.ThrowsExactly<FormatException>(() => Load("""
            <recorder initial="Idle"><triggers><trigger name="Begin" source="User" /></triggers><state name="Unknown" /></recorder>
            """));
    }

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
