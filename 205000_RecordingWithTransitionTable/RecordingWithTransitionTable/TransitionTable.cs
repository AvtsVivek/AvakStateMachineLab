namespace RecordingWithTransitionTable;

/// <summary>Centralizes the recorder's valid source, request, target, and guard combinations.</summary>
internal static class TransitionTable
{
    private static readonly IReadOnlyDictionary<(RecordingMode Source, RecordingRequest Request), TransitionDefinition> Definitions =
        new Dictionary<(RecordingMode Source, RecordingRequest Request), TransitionDefinition>
        {
            [(RecordingMode.Idle, RecordingRequest.Start)] =
                new(RecordingMode.Idle, RecordingRequest.Start, RecordingMode.Recording, RequiresStartPermission: true),
            [(RecordingMode.Recording, RecordingRequest.Pause)] =
                new(RecordingMode.Recording, RecordingRequest.Pause, RecordingMode.Paused, RequiresStartPermission: false),
            [(RecordingMode.Recording, RecordingRequest.Stop)] =
                new(RecordingMode.Recording, RecordingRequest.Stop, RecordingMode.Idle, RequiresStartPermission: false),
            [(RecordingMode.Paused, RecordingRequest.Resume)] =
                new(RecordingMode.Paused, RecordingRequest.Resume, RecordingMode.Recording, RequiresStartPermission: false),
            [(RecordingMode.Paused, RecordingRequest.Stop)] =
                new(RecordingMode.Paused, RecordingRequest.Stop, RecordingMode.Idle, RequiresStartPermission: false)
        };

    /// <summary>Looks up the transition for a source mode and request.</summary>
    /// <param name="source">The current mode.</param>
    /// <param name="request">The requested action.</param>
    /// <param name="definition">Receives the matching transition when one exists.</param>
    /// <returns>True when the table contains a matching transition.</returns>
    internal static bool TryGet(
        RecordingMode source,
        RecordingRequest request,
        out TransitionDefinition? definition)
        => Definitions.TryGetValue((source, request), out definition);
}
