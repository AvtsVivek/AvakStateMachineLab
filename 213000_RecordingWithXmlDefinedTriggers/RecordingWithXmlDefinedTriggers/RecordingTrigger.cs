namespace RecordingWithXmlDefinedTriggers;

/// <summary>Identifies an XML-defined trigger and the external component that emitted it.</summary>
public readonly record struct RecordingTrigger(string Name, string Source);
