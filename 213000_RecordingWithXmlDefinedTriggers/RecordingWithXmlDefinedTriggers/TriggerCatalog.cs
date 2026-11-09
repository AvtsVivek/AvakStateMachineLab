using System.Xml.Linq;

namespace RecordingWithXmlDefinedTriggers;

internal sealed class TriggerCatalog
{
    private readonly HashSet<RecordingTrigger> _triggers;

    private TriggerCatalog(HashSet<RecordingTrigger> triggers) => _triggers = triggers;

    internal static TriggerCatalog Load(XElement root)
    {
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
                TransitionTable.RequiredAttribute(element, "name"),
                TransitionTable.RequiredAttribute(element, "source"));
            if (!triggers.Add(trigger))
            {
                throw new FormatException($"Trigger '{trigger.Name}' from '{trigger.Source}' is declared more than once.");
            }
        }

        if (triggers.Count == 0)
        {
            throw new FormatException("The triggers element must declare at least one trigger.");
        }

        return new TriggerCatalog(triggers);
    }

    internal bool Contains(RecordingTrigger trigger) => _triggers.Contains(trigger);
}
