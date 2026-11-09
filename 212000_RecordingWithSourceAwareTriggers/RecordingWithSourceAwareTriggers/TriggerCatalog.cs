using System.Xml;
using System.Xml.Linq;

namespace RecordingWithSourceAwareTriggers;

/// <summary>Loads the allowed trigger name and source identities from XML.</summary>
public sealed class TriggerCatalog
{
    private readonly HashSet<RecordingTrigger> _triggers;

    private TriggerCatalog(HashSet<RecordingTrigger> triggers) => _triggers = triggers;

    public static TriggerCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        XElement root = XDocument.Load(reader).Root
            ?? throw new FormatException("The trigger catalog must have a triggers root element.");
        if (root.Name != "triggers")
        {
            throw new FormatException("The trigger catalog root element must be 'triggers'.");
        }

        TransitionTable.EnsureAttributes(root);
        HashSet<RecordingTrigger> triggers = [];
        foreach (XElement element in root.Elements())
        {
            if (element.Name != "trigger")
            {
                throw new FormatException("The triggers element can contain only trigger elements.");
            }

            TransitionTable.EnsureAttributes(element, "name", "source");
            if (element.HasElements)
            {
                throw new FormatException("Trigger elements cannot contain child elements.");
            }

            RecordingTrigger trigger = new(
                TransitionTable.ParseEnum<RecordingRequest>(TransitionTable.RequiredAttribute(element, "name"), "trigger"),
                TransitionTable.RequiredAttribute(element, "source"));
            if (!triggers.Add(trigger))
            {
                throw new FormatException($"Trigger '{trigger.Name}' from '{trigger.Source}' is declared more than once.");
            }
        }

        return new TriggerCatalog(triggers);
    }

    internal bool Contains(RecordingTrigger trigger) => _triggers.Contains(trigger);
}
