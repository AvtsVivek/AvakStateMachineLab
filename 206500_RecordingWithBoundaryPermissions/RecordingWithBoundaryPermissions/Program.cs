using RecordingWithBoundaryPermissions;

bool allowed = true;
var recorder = new Recorder(
    () => { Console.WriteLine("Check Start permission"); return allowed; },
    () => { Console.WriteLine("Check RecordingSession CanEnter"); return allowed; },
    () => { Console.WriteLine("Check RecordingSession CanExit"); return allowed; },
    Console.WriteLine);

Show(recorder, RecordingRequest.Start);
allowed = false;
Console.WriteLine("All permissions are now false.");
Show(recorder, RecordingRequest.Pause);
Show(recorder, RecordingRequest.Resume);
Show(recorder, RecordingRequest.Stop);
allowed = true;
Console.WriteLine("Permissions restored.");
Show(recorder, RecordingRequest.Stop);

/// <summary>Displays the request outcome after its lifecycle activity.</summary>
/// <param name="recorder">The recorder being demonstrated.</param>
/// <param name="request">The requested action.</param>
static void Show(Recorder recorder, RecordingRequest request)
{
    TransitionResult result = recorder.TryHandle(request);
    Console.WriteLine($"{request}: {result}; state: {recorder.CurrentState}");
}
