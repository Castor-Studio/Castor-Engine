using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class EncoderRetrievalTests : IDisposable
    {
        public EncoderRetrievalTests()
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
            Assert.Equal(nint.Zero, NativeVideoEncoderMethods.GetVideoEncoderHandle());
        }

        [Fact]
        public void GetAudioEncoderHandleShouldBeZeroBeforeConfigured()
        {
            Assert.Equal(nint.Zero, NativeVideoEncoderMethods.GetAudioEncoderHandle());
        }

        [StaFact]
        public void GetVideoEncoderHandleShouldBeNonZeroOnceConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode));

            Assert.NotEqual(nint.Zero, NativeVideoEncoderMethods.GetVideoEncoderHandle());
        }

        [StaFact]
        public void GetAudioEncoderHandleShouldBeNonZeroOnceConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            Assert.NotEqual(nint.Zero, NativeVideoEncoderMethods.GetAudioEncoderHandle());
        }

        // The actual proof that "the same encoder can be attached to more
        // than one output": retrieval never consumes or varies the handle,
        // so any number of independent callers get the same attachable
        // value back.
        [StaFact]
        public void GetVideoEncoderHandleShouldReturnTheSameValueOnRepeatedCalls()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode));

            var first = NativeVideoEncoderMethods.GetVideoEncoderHandle();
            var second = NativeVideoEncoderMethods.GetVideoEncoderHandle();
            var third = NativeVideoEncoderMethods.GetVideoEncoderHandle();

            Assert.Equal(first, second);
            Assert.Equal(first, third);
        }

        [StaFact]
        public void GetAudioEncoderHandleShouldReturnTheSameValueOnRepeatedCalls()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            var first = NativeVideoEncoderMethods.GetAudioEncoderHandle();
            var second = NativeVideoEncoderMethods.GetAudioEncoderHandle();

            Assert.Equal(first, second);
        }

        [StaFact]
        public void VideoAndAudioEncoderHandlesShouldBeDistinctValues()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode));
            NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            var videoHandle = NativeVideoEncoderMethods.GetVideoEncoderHandle();
            var audioHandle = NativeVideoEncoderMethods.GetAudioEncoderHandle();

            Assert.NotEqual(nint.Zero, videoHandle);
            Assert.NotEqual(nint.Zero, audioHandle);
            Assert.NotEqual(videoHandle, audioHandle);
        }

        [StaFact]
        public void EncoderHandlesShouldReturnToZeroAfterShutdown()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode));
            NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            EngineRuntime.Shutdown();

            Assert.Equal(nint.Zero, NativeVideoEncoderMethods.GetVideoEncoderHandle());
            Assert.Equal(nint.Zero, NativeVideoEncoderMethods.GetAudioEncoderHandle());
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
