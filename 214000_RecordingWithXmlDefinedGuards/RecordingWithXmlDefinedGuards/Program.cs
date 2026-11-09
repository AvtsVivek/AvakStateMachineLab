using RecordingWithXmlDefinedGuards;

bool startAllowed = false;
bool leaveRecordingAllowed = true;
bool enterPausedAllowed = true;

using FileStream configuration = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "recorder-transitions.xml"));
TransitionTable transitionTable = TransitionTable.Load(configuration);

var guardRegistry = new GuardRegistry();
guardRegistry.Register("StartPermission", () => { Console.WriteLine("Check StartPermission"); return startAllowed; });
guardRegistry.Register("RecordingExit", () => { Console.WriteLine("Check RecordingExit"); return leaveRecordingAllowed; });

var recorder = new Recorder(transitionTable, guardRegistry, 
    () => true,
    () => { Console.WriteLine("Check PausedEntry"); return enterPausedAllowed; },
    Console.WriteLine);

Send("PauseCapture");
Send("StartCapture");
Console.WriteLine("Start permission allowed.");
startAllowed = true;
Send("StartCapture");

Console.WriteLine("Recording exit denied: PauseCapture is blocked.");
leaveRecordingAllowed = false;
Send("PauseCapture");

Console.WriteLine("Recording exit allowed.");
leaveRecordingAllowed = true;
Send("PauseCapture");

Console.WriteLine("Paused entry denied.");
enterPausedAllowed = false;
Send("PauseCapture");

Console.WriteLine("Paused entry allowed.");
enterPausedAllowed = true;
Send("PauseCapture");
Send("ResumeCapture");
Send("StopCapture");

Console.WriteLine("PauseCapture from Device has a different configured outcome (no RecordingExit guard).");
startAllowed = true;
Send("StartCapture");
TransitionResult deviceResult = recorder.TryHandle(new RecordingTrigger("PauseCapture", "Device"));
Console.WriteLine($"PauseCapture/Device: {deviceResult}; state: {recorder.CurrentState}");

void Send(string name)
{
    TransitionResult result = recorder.TryHandle(new RecordingTrigger(name, "User"));
    Console.WriteLine($"{name}/User: {result}; state: {recorder.CurrentState}");
}
