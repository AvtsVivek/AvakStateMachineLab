using System.Xml;
using System.Xml.Linq;

namespace RecordingWithSourceAwareTriggers;

/// <summary>
/// Loads and validates the recorder's source-aware transition configuration.
/// Transitions are selected by current state and the trigger's name and source.
/// </summary>
public sealed class TransitionTable
{
    private readonly IReadOnlyDictionary<(RecordingMode State, RecordingTrigger Trigger), TransitionDefinition> _definitions;

    private TransitionTable(RecordingMode initialMode,
        IReadOnlyDictionary<(RecordingMode State, RecordingTrigger Trigger), TransitionDefinition> definitions)
    {
        InitialMode = initialMode;
        _definitions = definitions;
    }

    public RecordingMode InitialMode { get; }

    /// <summary>
    /// Loads a transition table and verifies every trigger against the supplied catalog.
    /// </summary>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="FormatException">The XML is well-formed but violates the recorder configuration contract.</exception>
    public static TransitionTable Load(Stream stream, TriggerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(catalog);

        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        XDocument document = XDocument.Load(reader);
        XElement root = document.Root ?? throw new FormatException("The configuration must have a recorder root element.");
        ValidatedConfiguration configuration = ValidateConfiguration(root, catalog);
        return new TransitionTable(configuration.InitialMode, configuration.Definitions);
    }

    private static ValidatedConfiguration ValidateConfiguration(XElement root, TriggerCatalog catalog)
    {
        if (root.Name != "recorder")
        {
            throw new FormatException("The configuration root element must be 'recorder'.");
        }

        EnsureAttributes(root, "initial");
        RecordingMode initialMode = ParseEnum<RecordingMode>(RequiredAttribute(root, "initial"), "initial state");
        List<XElement> stateElements = root.Elements().ToList();
        if (stateElements.Any(element => element.Name != "state"))
        {
            throw new FormatException("The recorder element can contain only state elements.");
        }

        HashSet<RecordingMode> declaredStates = [];
        Dictionary<(RecordingMode State, RecordingTrigger Trigger), TransitionDefinition> definitions = [];

        foreach (XElement stateElement in stateElements)
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

                RecordingRequest request = ParseEnum<RecordingRequest>(RequiredAttribute(transitionElement, "trigger"), "trigger");
                RecordingTrigger trigger = new(request, RequiredAttribute(transitionElement, "source"));
                if (!catalog.Contains(trigger))
                {
                    throw new FormatException($"Trigger '{trigger.Name}' from '{trigger.Source}' is not declared in the catalog.");
                }
                RecordingMode target = ParseEnum<RecordingMode>(RequiredAttribute(transitionElement, "target"), "transition target");
                string? guard = OptionalAttribute(transitionElement, "guard");
                bool requiresStartPermission = false;
                if (guard is not null)
                {
                    if (guard != "StartPermission" || source != RecordingMode.Idle || request != RecordingRequest.Start)
                    {
                        throw new FormatException($"Guard '{guard}' is not supported for transition '{source}' + '{request}'.");
                    }

                    requiresStartPermission = true;
                }

                if (!definitions.TryAdd((source, trigger), new TransitionDefinition(target, requiresStartPermission)))
                {
                    throw new FormatException($"Transition '{request}' from '{trigger.Source}' is declared more than once for state '{source}'.");
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

        return new ValidatedConfiguration(initialMode, definitions);
    }

    internal bool TryGet(RecordingMode state, RecordingTrigger trigger, out TransitionDefinition? definition)
        => _definitions.TryGetValue((state, trigger), out definition);

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

    private sealed record ValidatedConfiguration(
        RecordingMode InitialMode,
        Dictionary<(RecordingMode State, RecordingTrigger Trigger), TransitionDefinition> Definitions);
}
