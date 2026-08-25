using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class RecordingSubsystemTests : IDisposable
    {
        private readonly string _tempDirectory =
            Path.Combine(Path.GetTempPath(), "CastorEngineTests", Guid.NewGuid().ToString("N"));

        public RecordingSubsystemTests()
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

        [Fact]
        public void StartRecordingShouldRequireInitialization()
        {
            var config = NativeRecordingMethods.CreateConfig(CreateTempMkvPath());

            try
            {
                var result = NativeRecordingMethods.StartRecording(config);

                Assert.Equal(NativeRecordingResult.NotInitialized, result);
                Assert.False(IsRecordingActive());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        [StaFact]
        public void StartRecordingShouldRequireVideoConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var config = NativeRecordingMethods.CreateConfig(CreateTempMkvPath());

            try
            {
                var result = NativeRecordingMethods.StartRecording(config);

                Assert.Equal(NativeRecordingResult.VideoNotConfigured, result);
                Assert.False(IsRecordingActive());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        [StaFact]
        public void StartRecordingShouldRequireActiveScene()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            var config = NativeRecordingMethods.CreateConfig(CreateTempMkvPath());

            try
            {
                var result = NativeRecordingMethods.StartRecording(config);

                Assert.Equal(NativeRecordingResult.RecordingNoActiveScene, result);
                Assert.False(IsRecordingActive());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        // OBS's ffmpeg_muxer output (named explicitly in this issue) will
        // not start without an audio encoder bound, even for what the
        // issue otherwise scopes as a video-only recording - so starting a
        // recording auto-configures the audio subsystem and the AAC audio
        // encoder too, exactly as it already does for the video encoder,
        // rather than leaving recording permanently unable to start.
        [StaFact]
        public void StartRecordingShouldAutoConfigureSoftwareVideoEncoderAndAudioWhenNoneConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            Assert.False(NativeVideoEncoderMethods.IsVideoEncoderConfigured() != 0);
            Assert.False(NativeVideoEncoderMethods.IsAudioEncoderConfigured() != 0);

            var config = NativeRecordingMethods.CreateConfig(CreateTempMkvPath());

            try
            {
                var result = NativeRecordingMethods.StartRecording(config);

                Assert.Equal(NativeRecordingResult.Ok, result);
                Assert.True(IsRecordingActive());
                Assert.True(NativeVideoEncoderMethods.IsVideoEncoderConfigured() != 0);
                Assert.True(NativeVideoEncoderMethods.IsAudioEncoderConfigured() != 0);

                var selected = NativeVideoEncoderMethods.CreateInfo();
                Assert.NotEqual(0, NativeVideoEncoderMethods.GetSelectedVideoEncoder(ref selected));
                Assert.Equal(0, selected.IsHardware);

                Thread.Sleep(200);
                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StopRecording());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        // Only meaningful when this machine actually has a hardware
        // encoder - otherwise there is nothing to reject, and the assertion
        // is skipped rather than forcing one outcome. Mirrors the adaptive
        // pattern already used in VideoEncoderSubsystemTests.
        [StaFact]
        public void StartRecordingShouldRejectAnAlreadyConfiguredHardwareEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.HardwarePreferredSelectionMode));

            var selected = NativeVideoEncoderMethods.CreateInfo();
            NativeVideoEncoderMethods.GetSelectedVideoEncoder(ref selected);

            if (selected.IsHardware == 0)
            {
                return;
            }

            var config = NativeRecordingMethods.CreateConfig(CreateTempMkvPath());

            try
            {
                var result = NativeRecordingMethods.StartRecording(config);

                Assert.Equal(NativeRecordingResult.RecordingHardwareEncoderNotAllowed, result);
                Assert.False(IsRecordingActive());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        [StaFact]
        public void StartRecordingShouldRejectWhileAlreadyActive()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            var firstConfig = NativeRecordingMethods.CreateConfig(CreateTempMkvPath("first.mkv"));
            var secondConfig = NativeRecordingMethods.CreateConfig(CreateTempMkvPath("second.mkv"));

            try
            {
                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StartRecording(firstConfig));

                var result = NativeRecordingMethods.StartRecording(secondConfig);

                Assert.Equal(NativeRecordingResult.RecordingAlreadyActive, result);
                Assert.True(IsRecordingActive());

                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StopRecording());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(firstConfig);
                NativeRecordingMethods.FreeConfig(secondConfig);
            }
        }

        [Fact]
        public void StopRecordingShouldRejectWhenNotActive()
        {
            var result = NativeRecordingMethods.StopRecording();

            Assert.Equal(NativeRecordingResult.RecordingNotActive, result);
        }

        [StaFact]
        public void StopThenRestartShouldProduceASecondRecording()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            var firstPath = CreateTempMkvPath("first.mkv");
            var secondPath = CreateTempMkvPath("second.mkv");
            var firstConfig = NativeRecordingMethods.CreateConfig(firstPath);
            var secondConfig = NativeRecordingMethods.CreateConfig(secondPath);

            try
            {
                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StartRecording(firstConfig));
                Thread.Sleep(200);
                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StopRecording());
                Assert.False(IsRecordingActive());

                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StartRecording(secondConfig));
                Thread.Sleep(200);
                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StopRecording());

                Assert.True(File.Exists(firstPath));
                Assert.True(File.Exists(secondPath));
                Assert.True(new FileInfo(firstPath).Length > 0);
                Assert.True(new FileInfo(secondPath).Length > 0);
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(firstConfig);
                NativeRecordingMethods.FreeConfig(secondConfig);
            }
        }

        // The first real exercise of #35's documented release-ordering
        // contract: the output must stop and release itself before the
        // video encoder it references is released.
        [StaFact]
        public void ShutdownWhileRecordingShouldStopCleanlyAndFinalizeTheFile()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.CreateScene("wide");
            EngineRuntime.SwitchScene("wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));
            var path = CreateTempMkvPath();
            var config = NativeRecordingMethods.CreateConfig(path);

            try
            {
                Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StartRecording(config));
                Thread.Sleep(200);

                EngineRuntime.Shutdown();

                Assert.False(IsRecordingActive());
                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 0);
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
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
                EngineRuntime.Initialize(runtimeConfiguration);
                EngineRuntime.ConfigureVideo(videoConfiguration);
                EngineRuntime.CreateScene("wide");
                EngineRuntime.SwitchScene(
                    "wide", new EngineSceneTransitionConfiguration(EngineSceneTransitionType.Cut));

                var path = CreateTempMkvPath(fileName);
                var config = NativeRecordingMethods.CreateConfig(path);

                try
                {
                    Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StartRecording(config));
                    Assert.True(IsRecordingActive());

                    Thread.Sleep(200);

                    Assert.Equal(NativeRecordingResult.Ok, NativeRecordingMethods.StopRecording());
                    Assert.False(IsRecordingActive());
                    Assert.True(File.Exists(path));
                }
                finally
                {
                    NativeRecordingMethods.FreeConfig(config);
                }

                EngineRuntime.Shutdown();
            }
        }

        [Fact(Skip =
            "castor_engine_start_recording's OBS-rejection paths " +
            "(CASTOR_ENGINE_RECORDING_OUTPUT_CREATION_FAILED, " +
            "CASTOR_ENGINE_RECORDING_START_FAILED) cannot be reached " +
            "through the public API without a fault-injection seam in " +
            "OBS: the output type id is fixed and always registered once " +
            "modules are loaded, and a writable temp-directory path always " +
            "lets ffmpeg_muxer open the destination file. There is no " +
            "black-box way to force obs_output_create or obs_output_start " +
            "to fail for a valid configuration.")]
        public void StartRecordingShouldPropagateObsOutputFailures()
        {
        }

        private static bool IsRecordingActive()
        {
            return NativeRecordingMethods.IsRecordingActive() != 0;
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
