using RecordingWithGuards;

bool permission = false;
var recorder = new Recorder(() => permission, Console.WriteLine);

Console.WriteLine($"Start permission: {permission}");
Send(recorder, RecordingRequest.Start);
permission = true;
Console.WriteLine($"Start permission: {permission}");
Send(recorder, RecordingRequest.Start);
Send(recorder, RecordingRequest.Pause);
permission = false;
Console.WriteLine($"Start permission: {permission}");
Send(recorder, RecordingRequest.Resume);
Send(recorder, RecordingRequest.Stop);

/// <summary>Sends one request and displays its result after any lifecycle activity.</summary>
/// <param name="recorder">The recorder receiving the request.</param>
/// <param name="request">The transition trigger to demonstrate.</param>
static void Send(Recorder recorder, RecordingRequest request)
{
    bool accepted = recorder.TryHandle(request);
    Console.WriteLine($"{request}: {(accepted ? "accepted" : "rejected")}; state: {recorder.CurrentState}");
}
