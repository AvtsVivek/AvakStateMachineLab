using System.Text;
using System.Xml;

namespace RecordingWithXmlDefinedGuards.Tests;

[TestClass]
public sealed class XmlDefinedGuardTests
{
    private string LoadConfiguration()
    {
        // AppContext.BaseDirectory is bin\Debug\net10.0
        // We need to get to RecordingWithXmlDefinedGuards\recorder-transitions.xml
        string baseDir = AppContext.BaseDirectory;
        string? xmlPath = Path.Combine(baseDir, "recorder-transitions.xml");
        
        if (!File.Exists(xmlPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {xmlPath}");
        }
        
        return xmlPath;
    }

    private TransitionTable LoadTransitionTable()
    {
        using FileStream stream = File.OpenRead(LoadConfiguration());
        return TransitionTable.Load(stream);
    }

    private static MemoryStream XmlStream(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bytes);
    }

    private Recorder CreateRecorder(Func<bool>? startPermission = null, Func<bool>? recordingExit = null,
        Func<bool>? pausedEntry = null, List<string>? activity = null)
    {
        activity ??= [];
        TransitionTable table = LoadTransitionTable();
        var guardRegistry = new GuardRegistry();
        guardRegistry.Register("StartPermission", startPermission ?? (() => true));
        guardRegistry.Register("RecordingExit", recordingExit ?? (() => true));
        
        return new Recorder(table, guardRegistry, () => true, pausedEntry ?? (() => true), activity.Add);
    }

    [TestMethod]
    public void InitialState_IsIdle()
    {
        TransitionTable table = LoadTransitionTable();
        Assert.AreEqual(RecordingMode.Idle, table.InitialMode);
    }

    [TestMethod]
    public void Recorder_WithValidGuardRegistry_StartsInIdle()
    {
        var activity = new List<string>();
        TransitionTable table = LoadTransitionTable();
        var guardRegistry = new GuardRegistry();
        guardRegistry.Register("StartPermission", () => false);
        guardRegistry.Register("RecordingExit", () => true);

        var recorder = new Recorder(table, guardRegistry, () => true, () => true, activity.Add);
        
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(1, activity.Count);
        Assert.AreEqual("Enter Idle", activity[0]);
    }

    [TestMethod]
    public void GuardRegistry_Register_ThrowsWhenDuplicate()
    {
        var registry = new GuardRegistry();
        registry.Register("MyGuard", () => true);

        try
        {
            registry.Register("MyGuard", () => false);
            Assert.Fail("Should have thrown ArgumentException");
        }
        catch (ArgumentException ex)
        {
            Assert.IsTrue(ex.Message.Contains("already registered"));
        }
    }

    [TestMethod]
    public void GuardRegistry_Register_ThrowsForNullOrEmptyName()
    {
        var registry = new GuardRegistry();

        try
        {
            registry.Register("", () => true);
            Assert.Fail("Should have thrown ArgumentException");
        }
        catch (ArgumentException) { }

        try
        {
            registry.Register("  ", () => true);
            Assert.Fail("Should have thrown ArgumentException");
        }
        catch (ArgumentException) { }
    }

    [TestMethod]
    public void GuardRegistry_Register_ThrowsForNullEvaluate()
    {
        var registry = new GuardRegistry();

        try
        {
            registry.Register("Test", null!);
            Assert.Fail("Should have thrown ArgumentNullException");
        }
        catch (ArgumentNullException) { }
    }

    [TestMethod]
    public void StartTransition_WithGuardDenied_ReturnsGuardDenied()
    {
        var activity = new List<string>();
        Recorder recorder = CreateRecorder(startPermission: () => false, activity: activity);

        TransitionResult result = recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));

        Assert.AreEqual(TransitionResult.GuardDenied, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
        Assert.AreEqual(1, activity.Count);
    }

    [TestMethod]
    public void StartTransition_WithGuardAllowed_Transitioned()
    {
        var activity = new List<string>();
        Recorder recorder = CreateRecorder(startPermission: () => true, activity: activity);

        TransitionResult result = recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));

        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        Assert.AreEqual(3, activity.Count);
        Assert.AreEqual("Enter Idle", activity[0]);
        Assert.AreEqual("Exit Idle", activity[1]);
        Assert.AreEqual("Enter Recording", activity[2]);
    }

    [TestMethod]
    public void PauseTransition_WithRecordingExitGuardDenied_ReturnsGuardDenied()
    {
        var activity = new List<string>();
        Recorder recorder = CreateRecorder(
            startPermission: () => true, 
            recordingExit: () => false,
            activity: activity);

        recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));
        activity.Clear();

        TransitionResult result = recorder.TryHandle(new RecordingTrigger("PauseCapture", "User"));

        Assert.AreEqual(TransitionResult.GuardDenied, result);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
        Assert.AreEqual(0, activity.Count);
    }

    [TestMethod]
    public void PauseTransition_WithRecordingExitGuardAllowed_Transitioned()
    {
        var activity = new List<string>();
        Recorder recorder = CreateRecorder(
            startPermission: () => true, 
            recordingExit: () => true,
            activity: activity);

        recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));
        activity.Clear();

        TransitionResult result = recorder.TryHandle(new RecordingTrigger("PauseCapture", "User"));

        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Paused, recorder.CurrentState);
        Assert.AreEqual(2, activity.Count);
        Assert.AreEqual("Exit Recording", activity[0]);
        Assert.AreEqual("Enter Paused", activity[1]);
    }

    [TestMethod]
    public void PauseCapture_FromDevice_IgnoresRecordingExitGuard()
    {
        var activity = new List<string>();
        Recorder recorder = CreateRecorder(
            startPermission: () => true, 
            recordingExit: () => false,
            activity: activity);

        recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));
        activity.Clear();

        TransitionResult result = recorder.TryHandle(new RecordingTrigger("PauseCapture", "Device"));

        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Idle, recorder.CurrentState);
    }

    [TestMethod]
    public void InvalidTrigger_ReturnsInvalidRequest()
    {
        Recorder recorder = CreateRecorder();

        TransitionResult result = recorder.TryHandle(new RecordingTrigger("UnknownTrigger", "User"));

        Assert.AreEqual(TransitionResult.InvalidRequest, result);
    }

    [TestMethod]
    public void UnregisteredGuardName_ThrowsArgumentException()
    {
        using MemoryStream stream = XmlStream("""
            <recorder initial="Idle">
              <triggers><trigger name="Start" source="User" /></triggers>
              <state name="Idle"><transition trigger="Start" source="User" target="Recording" guard="MissingGuard" /></state>
              <state name="Recording" />
            </recorder>
            """);
        
        TransitionTable table = TransitionTable.Load(stream);
        var guardRegistry = new GuardRegistry();

        var recorder = new Recorder(table, guardRegistry, () => true, () => true, _ => { });

        try
        {
            recorder.TryHandle(new RecordingTrigger("Start", "User"));
            Assert.Fail("Should have thrown ArgumentException");
        }
        catch (ArgumentException ex)
        {
            Assert.IsTrue(ex.Message.Contains("MissingGuard"));
        }
    }

    [TestMethod]
    public void NestedRequest_ThrowsInvalidOperationException()
    {
        TransitionTable table = LoadTransitionTable();
        var guardRegistry = new GuardRegistry();
        guardRegistry.Register("StartPermission", () => true);
        guardRegistry.Register("RecordingExit", () => true);

        Recorder? recorder = null;
        var activity = new List<string>();

        recorder = new Recorder(table, guardRegistry, () => true, () => true, s =>
        {
            activity.Add(s);
            if (s == "Enter Recording" && recorder != null)
            {
                try
                {
                    recorder.TryHandle(new RecordingTrigger("PauseCapture", "User"));
                }
                catch (InvalidOperationException)
                {
                    activity.Add("Caught nested request");
                }
            }
        });

        recorder.TryHandle(new RecordingTrigger("StartCapture", "User"));

        Assert.IsTrue(activity.Contains("Caught nested request"));
    }

    [TestMethod]
    public void Recorder_WithNullTransitionTable_ThrowsArgumentNullException()
    {
        var guardRegistry = new GuardRegistry();
        try
        {
            new Recorder(null!, guardRegistry, () => true, () => true, _ => { });
            Assert.Fail("Should have thrown ArgumentNullException");
        }
        catch (ArgumentNullException) { }
    }

    [TestMethod]
    public void Recorder_WithNullGuardRegistry_ThrowsArgumentNullException()
    {
        TransitionTable table = LoadTransitionTable();
        try
        {
            new Recorder(table, null!, () => true, () => true, _ => { });
            Assert.Fail("Should have thrown ArgumentNullException");
        }
        catch (ArgumentNullException) { }
    }

    [TestMethod]
    public void CustomGuardName_CanBeUsed()
    {
        using MemoryStream stream = XmlStream("""
            <recorder initial="Idle">
              <triggers><trigger name="Start" source="User" /></triggers>
              <state name="Idle"><transition trigger="Start" source="User" target="Recording" guard="CustomPermission" /></state>
              <state name="Recording" />
            </recorder>
            """);
        
        TransitionTable table = TransitionTable.Load(stream);
        var guardRegistry = new GuardRegistry();
        guardRegistry.Register("CustomPermission", () => true);

        var recorder = new Recorder(table, guardRegistry, () => true, () => true, _ => { });
        
        TransitionResult result = recorder.TryHandle(new RecordingTrigger("Start", "User"));
        Assert.AreEqual(TransitionResult.Transitioned, result);
        Assert.AreEqual(RecordingMode.Recording, recorder.CurrentState);
    }

    [TestMethod]
    public void MultipleTransitionsWithDifferentGuards()
    {
        using MemoryStream stream = XmlStream("""
            <recorder initial="Idle">
              <triggers>
                <trigger name="Start" source="User" />
                <trigger name="Pause" source="User" />
              </triggers>
              <state name="Idle"><transition trigger="Start" source="User" target="Recording" guard="AllowStart" /></state>
              <state name="Recording"><transition trigger="Pause" source="User" target="Paused" guard="AllowPause" /></state>
              <state name="Paused" />
            </recorder>
            """);
        
        TransitionTable table = TransitionTable.Load(stream);
        var guardRegistry = new GuardRegistry();
        guardRegistry.Register("AllowStart", () => true);
        guardRegistry.Register("AllowPause", () => true);

        var recorder = new Recorder(table, guardRegistry, () => true, () => true, _ => { });
        
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("Start", "User")));
        Assert.AreEqual(TransitionResult.Transitioned, recorder.TryHandle(new("Pause", "User")));
        Assert.AreEqual(RecordingMode.Paused, recorder.CurrentState);
    }

    [TestMethod]
    public void GuardOrder_TransitionGuardBeforeStateGuards()
    {
        var guardCalls = new List<string>();
        using MemoryStream stream = XmlStream("""
            <recorder initial="Idle">
              <triggers><trigger name="Start" source="User" /></triggers>
              <state name="Idle"><transition trigger="Start" source="User" target="Recording" guard="TransitionGuard" /></state>
              <state name="Recording" />
            </recorder>
            """);
        
        TransitionTable table = TransitionTable.Load(stream);
        var guardRegistry = new GuardRegistry();
        guardRegistry.Register("TransitionGuard", () => { guardCalls.Add("TransitionGuard"); return true; });

        var recorder = new Recorder(table, guardRegistry, () => { guardCalls.Add("StateExit"); return true; }, () => { guardCalls.Add("StateEnter"); return true; }, _ => { });
        
        recorder.TryHandle(new("Start", "User"));

        Assert.AreEqual(1, guardCalls.Count);
        Assert.AreEqual("TransitionGuard", guardCalls[0]);
    }
}

