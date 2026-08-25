using System.Diagnostics;

namespace Castor.Engine.Tests
{
    /// <summary>
    /// End-to-end recording flow, from a cold engine to a finalized,
    /// decodable MKV file, exercised through the managed API exactly as a
    /// real caller would use it - including relying on
    /// <see cref="EngineRuntime.StartRecording"/> to auto-configure the
    /// video encoder, the audio subsystem, and the audio encoder, rather
    /// than configuring them explicitly first.
    /// </summary>
    public sealed class RecordingIntegrationTests : IDisposable
    {
        private readonly string _tempDirectory =
            Path.Combine(Path.GetTempPath(), "CastorEngineTests", Guid.NewGuid().ToString("N"));

        public RecordingIntegrationTests()
        {
            EngineRuntime.Shutdown();
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();

            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [StaFact]
        public void RecordingShouldProduceAPlayableMkvFileWithDecodableVideoAndAudio()
        {
            var path = Path.Combine(_tempDirectory, "integration.mkv");

            // 1. Initialize the engine (this also loads the OBS modules).
            EngineRuntime.Initialize(new EngineRuntimeConfiguration(AppContext.BaseDirectory));

            // 2. Configure the video subsystem.
            EngineRuntime.ConfigureVideo(
                new EngineVideoConfiguration(
                    baseWidth: 1280,
                    baseHeight: 720,
                    outputWidth: 1280,
                    outputHeight: 720,
                    framesPerSecondNumerator: 30,
                    framesPerSecondDenominator: 1));

            // 3. Create and activate a scene.
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

            // 4. Start a short recording. The video encoder, the audio
            // subsystem, and the AAC audio encoder are all configured
            // automatically here, in forced-software mode.
            EngineRuntime.StartRecording(new EngineRecordingConfiguration(path));
            Assert.True(EngineRuntime.IsRecordingActive);

            Thread.Sleep(500);

            // 5. Stop the recording and wait for finalization.
            EngineRuntime.StopRecording();
            Assert.False(EngineRuntime.IsRecordingActive);

            // 6. Verify the file exists and is not empty.
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);

            if (!TryFindFfprobe(out var ffprobePath))
            {
                // ffprobe isn't on PATH in this environment - skip the
                // decodability check rather than reporting a false
                // failure. GitHub-hosted Windows runners ship ffmpeg/
                // ffprobe in their standard tool cache, so this should
                // still run in CI; this is only a local-environment
                // fallback.
                return;
            }

            // 7. Verify the file contains a decodable H.264 video stream.
            Assert.Equal("h264", ProbeCodec(ffprobePath, path, "v:0"));

            // 8. Verify the file contains a decodable AAC audio stream -
            // beyond the original video-only scope, but always true here:
            // the ffmpeg_muxer output this feature uses cannot start
            // without an audio track (see the recording_subsystem commit
            // history for why).
            Assert.Equal("aac", ProbeCodec(ffprobePath, path, "a:0"));

            // 9. Complete engine cleanup already happened via StopRecording
            // above and Dispose()'s EngineRuntime.Shutdown() call.
        }

        [StaFact]
        public void DisplayCaptureShouldProduceAPlayableMkvWhenAnInteractiveDisplayIsAvailable()
        {
            var path = Path.Combine(_tempDirectory, "display-capture-integration.mkv");
            EngineRuntime.Initialize(new EngineRuntimeConfiguration(AppContext.BaseDirectory));
            EngineRuntime.ConfigureVideo(
                new EngineVideoConfiguration(1280, 720, 1280, 720, 30, 1));
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

            var displays = EngineRuntime.EnumerateDisplays();

            if (displays.Count == 0)
            {
                throw Xunit.Sdk.SkipException.ForSkip(
                    "No interactive display is available to exercise OBS display capture.");
            }

            var selectedDisplay = displays.FirstOrDefault(display => display.IsPrimary) ?? displays[0];
            EngineRuntime.ConfigureDisplayCapture(
                new EngineDisplayCaptureConfiguration("wide", selectedDisplay.Id, captureCursor: true));
            Assert.True(EngineRuntime.IsDisplayCaptureActive("wide"));

            EngineRuntime.StartRecording(new EngineRecordingConfiguration(path));
            Assert.True(EngineRuntime.IsRecordingActive);

            var replacementException = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureDisplayCapture(
                    new EngineDisplayCaptureConfiguration("wide", selectedDisplay.Id, captureCursor: false)));
            Assert.Contains("DisplayReconfigurationWhileRecording", replacementException.Message);

            Thread.Sleep(500);
            EngineRuntime.StopRecording();

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);

            if (TryFindFfprobe(out var ffprobePath))
            {
                Assert.Equal("h264", ProbeCodec(ffprobePath, path, "v:0"));
            }
        }

        private static bool TryFindFfprobe(out string ffprobePath)
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            foreach (var directory in pathVariable.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(directory, "ffprobe.exe");

                if (File.Exists(candidate))
                {
                    ffprobePath = candidate;
                    return true;
                }
            }

            ffprobePath = string.Empty;
            return false;
        }

        private static string ProbeCodec(string ffprobePath, string mediaPath, string streamSelector)
        {
            var startInfo = new ProcessStartInfo(ffprobePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-select_streams");
            startInfo.ArgumentList.Add(streamSelector);
            startInfo.ArgumentList.Add("-show_entries");
            startInfo.ArgumentList.Add("stream=codec_name");
            startInfo.ArgumentList.Add("-of");
            startInfo.ArgumentList.Add("csv=p=0");
            startInfo.ArgumentList.Add(mediaPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start ffprobe.");

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);

            return output.Trim();
        }
    }
}
