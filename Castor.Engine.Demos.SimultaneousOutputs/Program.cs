using Castor.Engine;

// Validation harness for https://github.com/Castor-Studio/Castor-Engine/issues/42:
// records to MKV and streams to RTMP at the same time, switching between 3
// scenes with transitions mid-session, while logging render-loop and RTMP
// delivery metrics at every step.

const string recordingPath = @"C:\recordings\simultaneous-outputs-validation.mkv";
var rtmpServer = Environment.GetEnvironmentVariable("CASTOR_VALIDATION_RTMP_SERVER") ?? "rtmp://127.0.0.1:1935/live";
var rtmpKey = Environment.GetEnvironmentVariable("CASTOR_VALIDATION_RTMP_KEY") ?? "castor-validation";
const int holdMilliseconds = 5000;

Directory.CreateDirectory(Path.GetDirectoryName(recordingPath)!);

void LogMetrics(string label)
{
    var render = EngineRuntime.GetRenderStats();
    var health = EngineRuntime.GetStreamingHealth();
    Console.WriteLine(
        $"[metrics] {label}: render total={render.TotalFrames} lagged={render.LaggedFrames} " +
        $"({render.LaggedFrameRatio:P3}); rtmp total={health.TotalFrames} dropped={health.DroppedFrames} " +
        $"({health.DroppedFrameRatio:P3})");
}

Console.WriteLine("Initializing engine...");
EngineRuntime.Initialize(new EngineRuntimeConfiguration(AppContext.BaseDirectory));
EngineRuntime.ConfigureVideo(new EngineVideoConfiguration(
    baseWidth: 1280, baseHeight: 720,
    outputWidth: 1280, outputHeight: 720,
    framesPerSecondNumerator: 30, framesPerSecondDenominator: 1));
EngineRuntime.ConfigureAudio(new EngineAudioConfiguration());

var displays = EngineRuntime.EnumerateDisplays();
Console.WriteLine($"Found {displays.Count} display(s):");
foreach (var display in displays)
{
    Console.WriteLine($"  - {display.Name} (id={display.Id}, primary={display.IsPrimary})");
}

if (displays.Count < 3)
{
    throw new InvalidOperationException($"This validation needs 3 displays, only {displays.Count} found.");
}

var sceneNames = new[] { "sceneA", "sceneB", "sceneC" };
for (var i = 0; i < 3; i++)
{
    EngineRuntime.CreateScene(sceneNames[i]);
    EngineRuntime.ConfigureDisplayCapture(new EngineDisplayCaptureConfiguration(sceneNames[i], displays[i].Id));
    Console.WriteLine($"Scene '{sceneNames[i]}' -> display '{displays[i].Name}'");
}

// The first switch after startup is always instant, regardless of the
// requested transition type - activate before recording/streaming start.
EngineRuntime.SwitchScene(sceneNames[0], new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

Console.WriteLine($"Starting recording to {recordingPath}...");
// Recording auto-configures a forced-software video encoder and the AAC
// audio encoder here (the primary pair). Streaming below starts second,
// while recording is already active, so it auto-configures its own
// isolated secondary pair instead of sharing the primary one - see
// castor_engine.cpp's encoder_slot tracking. Each output can then be
// stopped independently without hanging the other.
EngineRuntime.StartRecording(new EngineRecordingConfiguration(recordingPath));
Console.WriteLine($"IsRecordingActive={EngineRuntime.IsRecordingActive}");

Console.WriteLine($"Configuring streaming to {rtmpServer}/{rtmpKey}...");
EngineRuntime.ConfigureStreaming(new EngineStreamingConfiguration(rtmpServer, rtmpKey));
EngineRuntime.StartStreaming();

var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
while (DateTime.UtcNow < deadline)
{
    var status = EngineRuntime.GetStreamingStatus();
    if (status.State == EngineStreamingState.Live)
    {
        break;
    }
    if (status.State == EngineStreamingState.Failed)
    {
        throw new InvalidOperationException($"Streaming failed ({status.LastFailure}): {status.LastFailureMessage}");
    }
    Thread.Sleep(200);
}
Console.WriteLine($"Streaming state: {EngineRuntime.GetStreamingStatus().State}");

LogMetrics("both outputs live, before transitions");

var plan = new (string Scene, EngineSceneTransitionType Transition, uint DurationMs)[]
{
    (sceneNames[1], EngineSceneTransitionType.Fade, 500u),
    (sceneNames[2], EngineSceneTransitionType.Swipe, 500u),
    (sceneNames[0], EngineSceneTransitionType.Slide, 500u),
    (sceneNames[1], EngineSceneTransitionType.Fade, 800u),
};

Console.WriteLine($"Holding on '{sceneNames[0]}' for {holdMilliseconds}ms...");
Thread.Sleep(holdMilliseconds);

foreach (var step in plan)
{
    Console.WriteLine($"Switching -> '{step.Scene}' with {step.Transition} ({step.DurationMs}ms)...");
    EngineRuntime.SwitchScene(step.Scene, new EngineSceneTransitionConfiguration(step.Transition, step.DurationMs));
    LogMetrics($"immediately after switch to '{step.Scene}'");
    Thread.Sleep(holdMilliseconds);
}

LogMetrics("before stopping outputs");

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
Console.WriteLine("Stopping streaming...");
EngineRuntime.StopStreaming();
Console.WriteLine($"  StopStreaming returned after {stopwatch.ElapsedMilliseconds}ms");

stopwatch.Restart();
Console.WriteLine("Stopping recording...");
EngineRuntime.StopRecording();
Console.WriteLine($"  StopRecording returned after {stopwatch.ElapsedMilliseconds}ms");

Console.WriteLine("Shutting down...");
EngineRuntime.Shutdown();

Console.WriteLine($"Done. Recording written to {recordingPath}");
