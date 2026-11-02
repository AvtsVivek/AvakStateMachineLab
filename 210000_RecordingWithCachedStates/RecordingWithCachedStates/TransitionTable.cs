namespace RecordingWithCachedStates;

/// <summary>Defines valid transitions independently of state-wide entry and exit permissions.</summary>
internal static class TransitionTable
{
    private static readonly IReadOnlyDictionary<(RecordingMode Source, RecordingRequest Request), TransitionDefinition> Definitions =
        new Dictionary<(RecordingMode Source, RecordingRequest Request), TransitionDefinition>
        {
            [(RecordingMode.Idle, RecordingRequest.Start)] = new(RecordingMode.Recording, true),
            [(RecordingMode.Recording, RecordingRequest.Pause)] = new(RecordingMode.Paused, false),
            [(RecordingMode.Recording, RecordingRequest.Stop)] = new(RecordingMode.Idle, false),
            [(RecordingMode.Paused, RecordingRequest.Resume)] = new(RecordingMode.Recording, false),
            [(RecordingMode.Paused, RecordingRequest.Stop)] = new(RecordingMode.Idle, false)
        };

    /// <summary>Finds the transition before any permission is evaluated.</summary>
    /// <param name="source">The active mode.</param>
    /// <param name="request">The requested trigger.</param>
    /// <param name="definition">Receives the transition, if defined.</param>
    /// <returns>Whether this source/request pair has a transition.</returns>
    internal static bool TryGet(RecordingMode source, RecordingRequest request, out TransitionDefinition? definition)
        => Definitions.TryGetValue((source, request), out definition);
}
