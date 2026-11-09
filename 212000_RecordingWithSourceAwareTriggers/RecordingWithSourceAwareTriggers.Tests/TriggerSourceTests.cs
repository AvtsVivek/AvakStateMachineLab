using System.Text;
using System.Xml;
using RecordingWithSourceAwareTriggers;

namespace RecordingWithSourceAwareTriggers.Tests;

[TestClass]
public sealed class TriggerSourceTests
{
    /// <summary>
    /// Places two independent recorders in Recording, then varies only the source of Pause.
    /// User must select Paused and Device must select Idle, proving source participates in
    /// lookup even when the trigger name and current state are identical.
    /// </summary>
    [TestMethod]
    public void SameNameAndState_DifferentSources_SelectDifferentTransitions()
    {
        Recorder userRecorder = CreateConfiguredRecorder();
        Recorder deviceRecorder = CreateConfiguredRecorder();
        var start = new RecordingTrigger(RecordingRequest.Start, "User");
        Assert.AreEqual(TransitionResult.Transitioned, userRecorder.TryHandle(start));
        Assert.AreEqual(TransitionResult.Transitioned, deviceRecorder.TryHandle(start));

        Assert.AreEqual(TransitionResult.Transitioned, userRecorder.TryHandle(new(RecordingRequest.Pause, "User")));
        Assert.AreEqual(RecordingMode.Paused, userRecorder.CurrentState);
        Assert.AreEqual(TransitionResult.Transitioned, deviceRecorder.TryHandle(new(RecordingRequest.Pause, "Device")));
        Assert.AreEqual(RecordingMode.Idle, deviceRecorder.CurrentState);
    }

    /// <summary>
    /// Enters Recording, then sends Stop from an undeclared source pairing and Pause with
    /// a case-mismatched source. Both must return InvalidRequest and leave Recording active;
    /// neither name-only fallback nor case-insensitive source matching is permitted.
    /// </summary>
    [TestMethod]
    public void WrongOrUndeclaredSource_DoesNotFallbackToName()
    {
        Recorder recorder = CreateConfiguredRecorder();
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new(RecordingRequest.Start, "User")));

        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new(RecordingRequest.Stop, "Device")));
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new(RecordingRequest.Pause, "device")));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
    }

    /// <summary>
    /// Sends catalog-declared Pause/Device while the recorder is still Idle.
    /// Catalog membership authorizes a configuration identity, not a transition in every state,
    /// so dispatch must return InvalidRequest and preserve Idle.
    /// </summary>
    [TestMethod]
    public void DeclaredTriggerWithoutTransition_IsInvalid()
    {
        Recorder recorder = CreateConfiguredRecorder();
        Assert.AreEqual(TransitionResult.InvalidRequest, recorder.TryHandle(new(RecordingRequest.Pause, "Device")));
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
    }

    /// <summary>
    /// Exercises a default trigger, whose source is null, and an explicitly whitespace-only source.
    /// These are invalid arguments rather than unmatched identities, so dispatch must throw
    /// ArgumentNullException and ArgumentException respectively before transition lookup.
    /// </summary>
    [TestMethod]
    public void MissingTriggerSource_Throws()
    {
        Recorder recorder = CreateConfiguredRecorder();
        Assert.ThrowsExactly<ArgumentNullException>(() => recorder.TryHandle(default));
        Assert.ThrowsExactly<ArgumentException>(() => recorder.TryHandle(new(RecordingRequest.Start, " ")));
    }

    /// <summary>
    /// Loads two Pause identities and references both from the same initial state.
    /// Independent recorders must reach different targets for User and Device, proving both
    /// catalog entries are retained and usable, not merely accepted without an exception.
    /// </summary>
    [TestMethod]
    public void Catalog_AllowsSameNameFromDifferentSources()
    {
        using MemoryStream stream = XmlStream("""
            <recorder initial="Recording">
              <triggers>
                <trigger name="Pause" source="User" />
                <trigger name="Pause" source="Device" />
              </triggers>
              <state name="Idle" />
              <state name="Paused" />
              <state name="Recording">
                <transition trigger="Pause" source="User" target="Paused" />
                <transition trigger="Pause" source="Device" target="Idle" />
              </state>
            </recorder>
            """);
        TransitionTable table = TransitionTable.Load(stream);
        Recorder userRecorder = new(table, () => true, () => true, () => true, _ => { });
        Recorder deviceRecorder = new(table, () => true, () => true, () => true, _ => { });

        Assert.AreEqual(TransitionResult.Transitioned, userRecorder.TryHandle(new(RecordingRequest.Pause, "User")));
        Assert.AreEqual(RecordingMode.Paused, userRecorder.CurrentState);
        Assert.AreEqual(TransitionResult.Transitioned, deviceRecorder.TryHandle(new(RecordingRequest.Pause, "Device")));
        Assert.AreEqual(RecordingMode.Idle, deviceRecorder.CurrentState);
    }

    /// <summary>
    /// Runs one invalid catalog per row: duplicate identities, missing or empty sources,
    /// unknown or numeric trigger names, nested elements, extra attributes, unexpected children,
    /// and an incorrect root. Each is well-formed XML that must fail contract validation with FormatException.
    /// </summary>
    [TestMethod]
    [DataRow("""<triggers><trigger name="Pause" source="User" /><trigger name="Pause" source="User" /></triggers>""")]
    [DataRow("""<triggers><trigger name="Pause" /></triggers>""")]
    [DataRow("""<triggers><trigger name="Pause" source="" /></triggers>""")]
    [DataRow("""<triggers><trigger name="Unknown" source="User" /></triggers>""")]
    [DataRow("""<triggers><trigger name="0" source="User" /></triggers>""")]
    [DataRow("""<triggers><trigger name="Pause" source="User"><extra /></trigger></triggers>""")]
    [DataRow("""<triggers><trigger name="Pause" source="User" extra="x" /></triggers>""")]
    [DataRow("""<triggers><other /></triggers>""")]
    [DataRow("""<other />""")]
    public void Catalog_InvalidConfiguration_Throws(string xml)
    {
        Assert.ThrowsExactly<FormatException>(() => LoadCatalog(xml));
    }

    /// <summary>
    /// Supplies a recorder document with an internal entity used in the embedded catalog.
    /// The unified XML reader must reject the DTD with XmlException before catalog validation;
    /// even a local entity declaration must not be accepted by this configuration loader.
    /// </summary>
    [TestMethod]
    public void Catalog_DtdIsProhibited()
    {
        using MemoryStream stream = XmlStream("""
            <!DOCTYPE recorder [<!ENTITY x "User">]>
            <recorder initial="Idle">
              <triggers><trigger name="Pause" source="&x;" /></triggers>
              <state name="Idle" />
            </recorder>
            """);
        Assert.ThrowsExactly<XmlException>(() => TransitionTable.Load(stream));
    }

    /// <summary>
    /// Uses a catalog containing only Pause/User and Stop/User, then varies one transition per row.
    /// Missing, empty, or undeclared sources, including Stop/Device despite Stop/User being declared,
    /// must throw FormatException. A declared Idle self-target keeps target validation out of the scenario.
    /// </summary>
    [TestMethod]
    [DataRow("""<transition trigger="Pause" target="Idle" />""")]
    [DataRow("""<transition trigger="Pause" source="" target="Idle" />""")]
    [DataRow("""<transition trigger="Pause" source="Other" target="Idle" />""")]
    [DataRow("""<transition trigger="Stop" source="Device" target="Idle" />""")]
    public void Transition_RequiresExactCatalogIdentity(string transition)
    {
        using MemoryStream stream = XmlStream($"""
            <recorder initial="Idle">
              <triggers>
                <trigger name="Pause" source="User" />
                <trigger name="Stop" source="User" />
              </triggers>
              <state name="Idle">{transition}</state>
            </recorder>
            """);
        Assert.ThrowsExactly<FormatException>(() => TransitionTable.Load(stream));
    }

    /// <summary>
    /// Passes complete documents directly to the loader, without fixture composition.
    /// Missing or repeated catalogs, unsupported root/section content, duplicate states,
    /// and an initial state absent from the document must all fail with FormatException.
    /// </summary>
    [TestMethod]
    [DataRow("""<recorder initial="Idle"><state name="Idle" /></recorder>""")]
    [DataRow("""<recorder initial="Idle"><triggers /><triggers /><state name="Idle" /></recorder>""")]
    [DataRow("""<other initial="Idle"><triggers /><state name="Idle" /></other>""")]
    [DataRow("""<recorder initial="Idle" extra="x"><triggers /><state name="Idle" /></recorder>""")]
    [DataRow("""<recorder initial="Idle"><triggers extra="x" /><state name="Idle" /></recorder>""")]
    [DataRow("""<recorder initial="Idle"><triggers /><other /><state name="Idle" /></recorder>""")]
    [DataRow("""<recorder initial="Idle"><triggers /><state name="Idle" /><state name="Idle" /></recorder>""")]
    [DataRow("""<recorder initial="Idle"><triggers /></recorder>""")]
    public void Load_InvalidCombinedConfiguration_Throws(string xml)
    {
        using MemoryStream stream = XmlStream(xml);
        Assert.ThrowsExactly<FormatException>(() => TransitionTable.Load(stream));
    }

    /// <summary>
    /// Declares the catalog after a state that references it to verify document order does not
    /// affect validation. Dispatch must resolve the later declaration and enter Recording.
    /// </summary>
    [TestMethod]
    public void Load_CatalogAfterStates_ResolvesTriggerReferences()
    {
        using MemoryStream stream = XmlStream("""
            <recorder initial="Idle">
              <state name="Idle"><transition trigger="Start" source="User" target="Recording" /></state>
              <state name="Recording" />
              <triggers><trigger name="Start" source="User" /></triggers>
            </recorder>
            """);
        Recorder recorder = new(TransitionTable.Load(stream), () => true, () => true, () => true, _ => { });

        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new(RecordingRequest.Start, "User")));
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
    }

    private static Recorder CreateConfiguredRecorder()
    {
        using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "recorder-transitions.xml"));
        TransitionTable table = TransitionTable.Load(stream);
        return new Recorder(table, () => true, () => true, () => true, _ => { });
    }

    private static TransitionTable LoadCatalog(string xml)
    {
        using MemoryStream stream = XmlStream($"""<recorder initial="Idle">{xml}<state name="Idle" /></recorder>""");
        return TransitionTable.Load(stream);
    }

    private static MemoryStream XmlStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
