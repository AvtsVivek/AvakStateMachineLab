namespace RecordingWithStatePattern;

/// <summary>
/// Defines how the recorder handles a request while it is in a particular state.
/// </summary>
internal interface IRecorderState
{
    /// <summary>
    /// Gets the public mode represented by this state object.
    /// </summary>
    RecordingMode Mode { get; }

    /// <summary>
    /// Handles a request in this state and changes the recorder's state when allowed.
    /// </summary>
    /// <param name="recorder">The recorder whose state may change.</param>
    /// <param name="request">The request to handle.</param>
    /// <returns><see langword="true"/> if the request was accepted; otherwise, <see langword="false"/>.</returns>
    bool TryHandle(Recorder recorder, RecordingRequest request);
}
