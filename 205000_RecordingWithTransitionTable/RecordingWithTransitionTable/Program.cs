using RecordingWithTransitionTable;

bool permission = false;
var recorder = new Recorder(() => permission, Console.WriteLine);

Show(recorder, RecordingRequest.Start, "Start (permission denied)");
Show(recorder, RecordingRequest.Pause, "Pause (invalid in Idle)");
permission = true;
Show(recorder, RecordingRequest.Start, "Start (permission granted)");
Show(recorder, RecordingRequest.Pause, "Pause");
permission = false;
Show(recorder, RecordingRequest.Resume, "Resume (Start permission is false)");
Show(recorder, RecordingRequest.Stop, "Stop");

/// <summary>Sends a request and displays the result and current mode.</summary>
/// <param name="recorder">The recorder receiving the request.</param>
/// <param name="request">The requested action.</param>
/// <param name="description">A scenario label printed with the outcome.</param>
static void Show(Recorder recorder, RecordingRequest request, string description)
{
    TransitionResult result = recorder.TryHandle(request);
    Console.WriteLine($"{description}: {result}; state: {recorder.CurrentState}");
}
