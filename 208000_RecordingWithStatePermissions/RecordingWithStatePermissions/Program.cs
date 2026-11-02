using RecordingWithStatePermissions;

bool startAllowed = false;
bool leaveRecordingAllowed = true;
bool enterPausedAllowed = true;

var recorder = new Recorder(
    () => { Console.WriteLine("Check Start guard"); return startAllowed; },
    () => { Console.WriteLine("Check Recording.CanExit"); return leaveRecordingAllowed; },
    () => { Console.WriteLine("Check Paused.CanEnter"); return enterPausedAllowed; },
    Console.WriteLine);

Send(RecordingRequest.Pause);
Send(RecordingRequest.Start);
Console.WriteLine("Start guard allowed.");
startAllowed = true;
Send(RecordingRequest.Start);

Console.WriteLine("Recording exit denied: both Pause and Stop are blocked.");
leaveRecordingAllowed = false;
Send(RecordingRequest.Pause);
Send(RecordingRequest.Stop);

Console.WriteLine("Recording exit allowed; Paused entry denied.");
leaveRecordingAllowed = true;
enterPausedAllowed = false;
Send(RecordingRequest.Pause);

Console.WriteLine("Paused entry allowed.");
enterPausedAllowed = true;
Send(RecordingRequest.Pause);
Console.WriteLine("All policies now false: Resume does not use any of them.");
startAllowed = false;
leaveRecordingAllowed = false;
enterPausedAllowed = false;
Send(RecordingRequest.Resume);

Console.WriteLine("Recording exit allowed again.");
leaveRecordingAllowed = true;
Send(RecordingRequest.Stop);

void Send(RecordingRequest request)
{
    TransitionResult result = recorder.TryHandle(request);
    Console.WriteLine($"{request}: {result}; state: {recorder.CurrentState}");
}
