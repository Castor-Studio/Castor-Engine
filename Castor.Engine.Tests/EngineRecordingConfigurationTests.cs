using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class EngineRecordingConfigurationTests : IDisposable
    {
        private readonly string _tempDirectory =
            Path.Combine(Path.GetTempPath(), "CastorEngineTests", Guid.NewGuid().ToString("N"));

        public EngineRecordingConfigurationTests()
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
        public void StartRecordingShouldRecordAndProduceAFile()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            var path = CreateTempMkvPath();

            EngineRuntime.StartRecording(new EngineRecordingConfiguration(path));

            Assert.True(EngineRuntime.IsRecordingActive);

            Thread.Sleep(200);
            EngineRuntime.StopRecording();

            Assert.False(EngineRuntime.IsRecordingActive);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }

        [Fact]
        public void StartRecordingShouldRejectNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(
                () => EngineRuntime.StartRecording(null!));
        }

        [Fact]
        public void StartRecordingShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.StartRecording(new EngineRecordingConfiguration(CreateTempMkvPath())));

            Assert.Contains("NotInitialized", exception.Message);
            Assert.False(EngineRuntime.IsRecordingActive);
        }

        [StaFact]
        public void StartRecordingShouldRequireActiveScene()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.StartRecording(new EngineRecordingConfiguration(CreateTempMkvPath())));

            Assert.Contains("RecordingNoActiveScene", exception.Message);
            Assert.False(EngineRuntime.IsRecordingActive);
        }

        [StaFact]
        public void StartRecordingShouldRejectWhileAlreadyActive()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            EngineRuntime.StartRecording(new EngineRecordingConfiguration(CreateTempMkvPath("first.mkv")));

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.StartRecording(new EngineRecordingConfiguration(CreateTempMkvPath("second.mkv"))));

            Assert.Contains("RecordingAlreadyActive", exception.Message);
            Assert.True(EngineRuntime.IsRecordingActive);

            EngineRuntime.StopRecording();
        }

        [Fact]
        public void StopRecordingShouldRequireActiveRecording()
        {
            var exception = Assert.Throws<InvalidOperationException>(EngineRuntime.StopRecording);

            Assert.Contains("RecordingNotActive", exception.Message);
        }

        [StaFact]
        public void ShutdownShouldClearRecordingState()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            EngineRuntime.StartRecording(new EngineRecordingConfiguration(CreateTempMkvPath()));

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsRecordingActive);
        }

        [StaFact]
        public void ManagedAndNativeRecordingStateShouldStayConsistent()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

            Assert.Equal(EngineRuntime.IsRecordingActive, NativeRecordingMethods.IsRecordingActive() != 0);

            EngineRuntime.StartRecording(new EngineRecordingConfiguration(CreateTempMkvPath()));

            Assert.Equal(EngineRuntime.IsRecordingActive, NativeRecordingMethods.IsRecordingActive() != 0);

            EngineRuntime.StopRecording();

            Assert.Equal(EngineRuntime.IsRecordingActive, NativeRecordingMethods.IsRecordingActive() != 0);
        }

        [StaFact]
        public void RecordingLifecycleShouldRunEndToEndAndRepeat()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var videoConfiguration = CreateVideoConfiguration();

            RunLifecycle("first.mkv");
            RunLifecycle("second.mkv");

            void RunLifecycle(string fileName)
            {
                // 1. Initialize the engine.
                EngineRuntime.Initialize(runtimeConfiguration);

                // 2. Configure the video subsystem and activate a scene.
                EngineRuntime.ConfigureVideo(videoConfiguration);
                EngineRuntime.CreateScene("wide");
                EngineRuntime.SwitchScene(
                    "wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

                // 3. Start recording to a fresh destination.
                var path = CreateTempMkvPath(fileName);
                EngineRuntime.StartRecording(new EngineRecordingConfiguration(path));
                Assert.True(EngineRuntime.IsRecordingActive);

                Thread.Sleep(200);

                // 4. Stop and verify the file was produced.
                EngineRuntime.StopRecording();
                Assert.False(EngineRuntime.IsRecordingActive);
                Assert.True(File.Exists(path));

                // 5. Shut down the engine.
                EngineRuntime.Shutdown();

                // 6. The caller repeats the lifecycle by calling RunLifecycle() again.
            }
        }

        private string CreateTempMkvPath(string fileName = "recording.mkv")
        {
            return Path.Combine(_tempDirectory, fileName);
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }

        private static EngineVideoConfiguration CreateVideoConfiguration()
        {
            return new EngineVideoConfiguration(
                baseWidth: 1280,
                baseHeight: 720,
                outputWidth: 1280,
                outputHeight: 720,
                framesPerSecondNumerator: 30,
                framesPerSecondDenominator: 1);
        }
    }
}
