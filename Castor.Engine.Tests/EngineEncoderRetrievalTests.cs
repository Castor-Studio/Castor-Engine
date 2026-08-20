namespace Castor.Engine.Tests
{
    public sealed class EngineEncoderRetrievalTests : IDisposable
    {
        public EngineEncoderRetrievalTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void GetVideoEncoderHandleShouldBeZeroBeforeConfigured()
        {
            Assert.Equal(nint.Zero, EngineRuntime.GetVideoEncoderHandle());
        }

        [Fact]
        public void GetAudioEncoderHandleShouldBeZeroBeforeConfigured()
        {
            Assert.Equal(nint.Zero, EngineRuntime.GetAudioEncoderHandle());
        }

        [StaFact]
        public void EncoderHandlesShouldBeNonZeroAndDistinctOnceConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            EngineRuntime.ConfigureVideoEncoder(
                new EngineVideoEncoderConfiguration(
                    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced));
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

            var videoHandle = EngineRuntime.GetVideoEncoderHandle();
            var audioHandle = EngineRuntime.GetAudioEncoderHandle();

            Assert.NotEqual(nint.Zero, videoHandle);
            Assert.NotEqual(nint.Zero, audioHandle);
            Assert.NotEqual(videoHandle, audioHandle);
        }

        [StaFact]
        public void GetVideoEncoderHandleShouldReturnTheSameValueOnRepeatedCalls()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureVideoEncoder(
                new EngineVideoEncoderConfiguration(
                    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced));

            var first = EngineRuntime.GetVideoEncoderHandle();
            var second = EngineRuntime.GetVideoEncoderHandle();

            Assert.Equal(first, second);
        }

        [StaFact]
        public void EncoderHandlesShouldReturnToZeroAfterShutdown()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            EngineRuntime.ConfigureVideoEncoder(
                new EngineVideoEncoderConfiguration(
                    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced));
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

            EngineRuntime.Shutdown();

            Assert.Equal(nint.Zero, EngineRuntime.GetVideoEncoderHandle());
            Assert.Equal(nint.Zero, EngineRuntime.GetAudioEncoderHandle());
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }

        private static EngineAudioConfiguration CreateAudioConfiguration()
        {
            return new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo);
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
