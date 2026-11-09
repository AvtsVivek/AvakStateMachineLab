using System.Xml;
using System.Xml.Linq;

namespace RecordingWithXmlDefinedGuards;

/// <summary>Loads XML-defined trigger names, guard names, and source-aware state transitions.</summary>
public sealed class TransitionTable
{
    private readonly IReadOnlyDictionary<(RecordingMode State, RecordingTrigger Trigger), TransitionDefinition> _definitions;
    private readonly HashSet<string> _declaredGuards;

    private TransitionTable(RecordingMode initialMode,
        IReadOnlyDictionary<(RecordingMode State, RecordingTrigger Trigger), TransitionDefinition> definitions,
        HashSet<string> declaredGuards)
    {
        InitialMode = initialMode;
        _definitions = definitions;
        _declaredGuards = declaredGuards;
    }

    public RecordingMode InitialMode { get; }

    /// <summary>Loads one validated recorder configuration from XML.</summary>
    public static TransitionTable Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        XElement root = XDocument.Load(reader).Root
            ?? throw new FormatException("The configuration must have a recorder root element.");
        return Load(root);
    }

    private static TransitionTable Load(XElement root)
    {
        if (root.Name != "recorder")
        {
            throw new FormatException("The configuration root element must be 'recorder'.");
        }

        EnsureAttributes(root, "initial");
        RecordingMode initialMode = ParseEnum<RecordingMode>(RequiredAttribute(root, "initial"), "initial state");
        if (root.Elements().Any(element => element.Name != "triggers" && element.Name != "state"))
        {
            throw new FormatException("The recorder element can contain only a triggers element and state elements.");
        }

        List<XElement> catalogs = root.Elements("triggers").ToList();
        if (catalogs.Count != 1)
        {
            throw new FormatException("The recorder element requires exactly one triggers element.");
        }

        TriggerCatalog catalog = TriggerCatalog.Load(catalogs[0]);
        HashSet<RecordingMode> declaredStates = [];
        HashSet<string> declaredGuards = [];
        Dictionary<(RecordingMode State, RecordingTrigger Trigger), TransitionDefinition> definitions = [];

        foreach (XElement stateElement in root.Elements("state"))
        {
            EnsureAttributes(stateElement, "name");
            RecordingMode source = ParseEnum<RecordingMode>(RequiredAttribute(stateElement, "name"), "state name");
            if (!declaredStates.Add(source))
            {
                throw new FormatException($"State '{source}' is declared more than once.");
            }

            foreach (XElement transitionElement in stateElement.Elements())
            {
                if (transitionElement.Name != "transition")
                {
                    throw new FormatException($"State '{source}' can contain only transition elements.");
                }

                EnsureAttributes(transitionElement, "trigger", "source", "target", "guard");
                if (transitionElement.HasElements)
                {
                    throw new FormatException("Transition elements cannot contain child elements.");
                }

                RecordingTrigger trigger = new(
                    RequiredAttribute(transitionElement, "trigger"),
                    RequiredAttribute(transitionElement, "source"));
                if (!catalog.Contains(trigger))
                {
                    throw new FormatException($"Trigger '{trigger.Name}' from '{trigger.Source}' is not declared in the catalog.");
                }

                RecordingMode target = ParseEnum<RecordingMode>(RequiredAttribute(transitionElement, "target"), "transition target");
                string? guardName = OptionalAttribute(transitionElement, "guard");
                
                if (guardName is not null)
                {
                    declaredGuards.Add(guardName);
                }

                if (!definitions.TryAdd((source, trigger), new TransitionDefinition(target, guardName)))
                {
                    throw new FormatException($"Transition '{trigger.Name}' from '{trigger.Source}' is declared more than once for state '{source}'.");
                }
            }
        }

        if (!declaredStates.Contains(initialMode))
        {
            throw new FormatException($"Initial state '{initialMode}' is not declared.");
        }

        foreach (((RecordingMode source, RecordingTrigger trigger), TransitionDefinition definition) in definitions)
        {
            if (!declaredStates.Contains(definition.Target))
            {
                throw new FormatException($"Target state '{definition.Target}' for '{source}' + '{trigger.Name}' from '{trigger.Source}' is not declared.");
            }
        }

        return new TransitionTable(initialMode, definitions, declaredGuards);
    }

    internal bool TryGet(RecordingMode state, RecordingTrigger trigger, out TransitionDefinition? definition)
        => _definitions.TryGetValue((state, trigger), out definition);

    internal IReadOnlyCollection<string> DeclaredGuards => _declaredGuards;

    internal static TEnum ParseEnum<TEnum>(string value, string description) where TEnum : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: false, out TEnum parsed)
            && string.Equals(value, parsed.ToString(), StringComparison.Ordinal))
        {
            return parsed;
        }

        throw new FormatException($"Unknown {description} '{value}'.");
    }

    internal static void EnsureAttributes(XElement element, params string[] allowedNames)
    {
        HashSet<string> allowed = new(allowedNames, StringComparer.Ordinal);
        if (element.Attributes().Any(attribute => attribute.IsNamespaceDeclaration
                || attribute.Name.Namespace != XNamespace.None
                || !allowed.Contains(attribute.Name.LocalName)))
        {
            throw new FormatException($"Element '{element.Name.LocalName}' contains an unsupported attribute.");
        }
    }

    internal static string RequiredAttribute(XElement element, string name)
    {
        string? value = OptionalAttribute(element, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Element '{element.Name.LocalName}' requires a non-empty '{name}' attribute.");
        }

        return value;
    }

    private static string? OptionalAttribute(XElement element, string name)
        => (string?)element.Attribute(name);
}
