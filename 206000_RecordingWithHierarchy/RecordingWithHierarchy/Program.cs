using RecordingWithHierarchy;

bool canStart = true;
bool canEnterSession = true;
bool canExitSession = true;
var recorder = new Recorder(
    () => { Console.WriteLine("Check Start permission"); return canStart; },
    () => { Console.WriteLine("Check RecordingSession CanEnter"); return canEnterSession; },
    () => { Console.WriteLine("Check RecordingSession CanExit"); return canExitSession; },
    Console.WriteLine);

Show(recorder, RecordingRequest.Start, "Start");
Show(recorder, RecordingRequest.Pause, "Pause");
Show(recorder, RecordingRequest.Resume, "Resume");
canExitSession = false;
Show(recorder, RecordingRequest.Stop, "Stop while parent CanExit is false");
canExitSession = true;
Show(recorder, RecordingRequest.Stop, "Stop after parent CanExit is allowed");

/// <summary>Sends a request and displays the result with the active leaf mode.</summary>
/// <param name="recorder">The recorder receiving the request.</param>
/// <param name="request">The requested action.</param>
/// <param name="description">The scenario label shown in the console.</param>
static void Show(Recorder recorder, RecordingRequest request, string description)
{
    TransitionResult result = recorder.TryHandle(request);
    Console.WriteLine($"{description}: {result}; state: {recorder.CurrentState}");
}
