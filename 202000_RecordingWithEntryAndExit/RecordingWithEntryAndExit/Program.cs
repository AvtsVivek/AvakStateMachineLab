using RecordingWithEntryAndExit;

Recorder recorder = new(Console.WriteLine);
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
    Console.WriteLine($"Request: {request}");
    bool accepted = recorder.TryHandle(request);
    string outcome = accepted ? "Accepted" : "Rejected";
    Console.WriteLine($"{outcome}; current state: {recorder.CurrentState}");
}
