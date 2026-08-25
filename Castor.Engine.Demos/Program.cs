using Castor.Engine;

const string outputPath = @"C:\recordings\three-screens-demo.mkv";
const int holdMilliseconds = 4000;

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

Console.WriteLine("Initializing engine...");
EngineRuntime.Initialize(new EngineRuntimeConfiguration(AppContext.BaseDirectory));
EngineRuntime.ConfigureVideo(new EngineVideoConfiguration(
    baseWidth: 1920, baseHeight: 1080,
    outputWidth: 1920, outputHeight: 1080,
    framesPerSecondNumerator: 30, framesPerSecondDenominator: 1));

var displays = EngineRuntime.EnumerateDisplays();
Console.WriteLine($"Found {displays.Count} display(s):");
foreach (var display in displays)
{
    Console.WriteLine($"  - {display.Name} (id={display.Id}, primary={display.IsPrimary})");
}

if (displays.Count < 3)
{
    throw new InvalidOperationException($"This demo needs 3 displays, only {displays.Count} found.");
}

// One scene per screen.
var sceneNames = new[] { "screen1", "screen2", "screen3" };
for (var i = 0; i < 3; i++)
{
    EngineRuntime.CreateScene(sceneNames[i]);
    EngineRuntime.ConfigureDisplayCapture(new EngineDisplayCaptureConfiguration(sceneNames[i], displays[i].Id));
    Console.WriteLine($"Scene '{sceneNames[i]}' -> display '{displays[i].Name}'");
}

// Activate the first scene before recording starts - the very first switch
// is always instant, regardless of the requested transition type.
EngineRuntime.SwitchScene("screen1", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

Console.WriteLine($"Recording to {outputPath}...");
EngineRuntime.StartRecording(new EngineRecordingConfiguration(outputPath));

Console.WriteLine("screen1 on air (fixed color for a few seconds)...");
Thread.Sleep(holdMilliseconds);

Console.WriteLine("Switching screen1 -> screen2 with Fade (500 ms)...");
EngineRuntime.SwitchScene("screen2", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Fade, 500));
Thread.Sleep(holdMilliseconds);

Console.WriteLine("Switching screen2 -> screen3 with Swipe (500 ms)...");
EngineRuntime.SwitchScene("screen3", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Swipe, 500));
Thread.Sleep(holdMilliseconds);

Console.WriteLine("Stopping recording...");
EngineRuntime.StopRecording();

Console.WriteLine("Shutting down...");
EngineRuntime.Shutdown();

Console.WriteLine($"Done. File written to {outputPath}");
