using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class EngineStreamingTests : IDisposable
    {
        public EngineStreamingTests() => EngineRuntime.Shutdown();
        public void Dispose() => EngineRuntime.Shutdown();

        [Fact]
        public void ConfigureStreamingShouldRejectNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => EngineRuntime.ConfigureStreaming(null!));
        }

        [Fact]
        public void ConfigureStreamingShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureStreaming(CreateStreamingConfiguration()));
            Assert.Contains("NotInitialized", exception.Message);
        }

        [StaFact]
        public void ConfigureStreamingShouldSucceedBeforeMediaSubsystemsAreReady()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureStreaming(CreateStreamingConfiguration());

            var status = EngineRuntime.GetStreamingStatus();
            var nativeStatus = NativeStreamingMethods.CreateStatus();
            Assert.Equal(NativeStreamingResult.Ok, NativeStreamingMethods.GetStatus(ref nativeStatus));
            Assert.Equal(EngineStreamingState.Idle, status.State);
            Assert.Equal((uint)status.State, nativeStatus.State);
            Assert.False(status.HasFailure);
        }

        [StaFact]
        public void StartStreamingShouldReportEachMissingPrerequisite()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureStreaming(CreateStreamingConfiguration());

            Assert.Contains("VideoNotConfigured", Assert.Throws<InvalidOperationException>(
                EngineRuntime.StartStreaming).Message);

            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            Assert.Contains("AudioNotConfigured", Assert.Throws<InvalidOperationException>(
                EngineRuntime.StartStreaming).Message);

            EngineRuntime.ConfigureAudio(new EngineAudioConfiguration());
            Assert.Contains("StreamingEncodersNotConfigured", Assert.Throws<InvalidOperationException>(
                EngineRuntime.StartStreaming).Message);

            EngineRuntime.ConfigureVideoEncoder(new EngineVideoEncoderConfiguration(
                selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced));
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);
            Assert.Contains("StreamingNoActiveScene", Assert.Throws<InvalidOperationException>(
                EngineRuntime.StartStreaming).Message);
        }

        [StaFact]
        public void StreamingHealthShouldRequireAnActiveSession()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureStreaming(CreateStreamingConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(EngineRuntime.GetStreamingHealth);
            Assert.Contains("StreamingNotActive", exception.Message);
        }

        [Fact]
        public void StopStreamingShouldRequireAnActiveSession()
        {
            var exception = Assert.Throws<InvalidOperationException>(EngineRuntime.StopStreaming);
            Assert.Contains("StreamingNotActive", exception.Message);
        }

        [StaFact]
        public void ShutdownShouldClearConfigurationAndStatus()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureStreaming(CreateStreamingConfiguration());
            EngineRuntime.Shutdown();

            var status = EngineRuntime.GetStreamingStatus();
            Assert.Equal(EngineStreamingState.Idle, status.State);
            Assert.False(status.HasFailure);

            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            Assert.Contains("StreamingNotConfigured", Assert.Throws<InvalidOperationException>(
                EngineRuntime.StartStreaming).Message);
        }

        private static EngineStreamingConfiguration CreateStreamingConfiguration() =>
            new("rtmp://127.0.0.1:1935/live", "unit-test-key");

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration() =>
            new(AppContext.BaseDirectory);

        private static EngineVideoConfiguration CreateVideoConfiguration() =>
            new(1280, 720, 1280, 720, 30, 1);
    }
}
