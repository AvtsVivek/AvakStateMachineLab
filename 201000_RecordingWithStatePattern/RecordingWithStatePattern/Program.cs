using RecordingWithStatePattern;

Recorder recorder = new();
Console.WriteLine($"Initial state: {recorder.CurrentState}");

RecordingRequest[] requests =
[
    RecordingRequest.Pause,
    RecordingRequest.Start,
    RecordingRequest.Pause,
    RecordingRequest.Resume,
    RecordingRequest.Stop,
    RecordingRequest.Start,
    RecordingRequest.Pause,
    RecordingRequest.Stop
];

foreach (RecordingRequest request in requests)
{
    RecordingMode previousState = recorder.CurrentState;
    bool accepted = recorder.TryHandle(request);
    string outcome = accepted ? "Accepted" : "Rejected";
    Console.WriteLine($"{previousState} + {request} -> {recorder.CurrentState} ({outcome})");
}
