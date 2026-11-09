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
    /// Loads two Pause declarations with different sources and expects loading to complete without throwing.
    /// This acceptance case establishes that uniqueness is defined by the full name/source pair, not name alone.
    /// </summary>
    [TestMethod]
    public void Catalog_AllowsSameNameFromDifferentSources()
    {
        LoadCatalog("""<triggers><trigger name="Pause" source="User" /><trigger name="Pause" source="Device" /></triggers>""");
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
    /// Supplies a document type declaration with an internal entity used as a source value.
    /// The XML reader must reject the DTD with XmlException before catalog validation;
    /// even a local entity declaration must not be accepted by this configuration loader.
    /// </summary>
    [TestMethod]
    public void Catalog_DtdIsProhibited()
    {
        Assert.ThrowsExactly<XmlException>(() => LoadCatalog("""<!DOCTYPE triggers [<!ENTITY x "User">]><triggers><trigger name="Pause" source="&x;" /></triggers>"""));
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
        TriggerCatalog catalog = LoadCatalog("""<triggers><trigger name="Pause" source="User" /><trigger name="Stop" source="User" /></triggers>""");
        using MemoryStream stream = XmlStream($"""<recorder initial="Idle"><state name="Idle">{transition}</state></recorder>""");
        Assert.ThrowsExactly<FormatException>(() => TransitionTable.Load(stream, catalog));
    }

    /// <summary>
    /// Checks two independent null-argument contracts: the transition loader needs a catalog,
    /// and the catalog loader needs an input stream. Both must throw ArgumentNullException
    /// before attempting XML validation; null-forgiving syntax enables these intentional invalid inputs.
    /// </summary>
    [TestMethod]
    public void NullCatalog_IsRejected()
    {
        using MemoryStream stream = XmlStream("<recorder initial=\"Idle\" />");
        Assert.ThrowsExactly<ArgumentNullException>(() => TransitionTable.Load(stream, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => TriggerCatalog.Load(null!));
    }

    private static Recorder CreateConfiguredRecorder()
    {
        using FileStream catalogStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "recorder-triggers.xml"));
        TriggerCatalog catalog = TriggerCatalog.Load(catalogStream);
        using FileStream transitionsStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "recorder-transitions.xml"));
        TransitionTable table = TransitionTable.Load(transitionsStream, catalog);
        return new Recorder(table, () => true, () => true, () => true, _ => { });
    }

    private static TriggerCatalog LoadCatalog(string xml)
    {
        using MemoryStream stream = XmlStream(xml);
        return TriggerCatalog.Load(stream);
    }

    private static MemoryStream XmlStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
