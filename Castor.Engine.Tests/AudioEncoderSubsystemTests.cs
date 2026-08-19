using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class AudioEncoderSubsystemTests : IDisposable
    {
        public AudioEncoderSubsystemTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ConfigureAudioEncoderShouldRequireInitialization()
        {
            var result = NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            Assert.Equal(NativeVideoEncoderResult.NotInitialized, result);
            Assert.Contains("must be initialized", NativeVideoEncoderMethods.GetLastErrorMessage());
            Assert.False(IsAudioEncoderConfigured());
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldRequireAudioConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var result = NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            Assert.Equal(NativeVideoEncoderResult.AudioNotConfigured, result);
            Assert.Contains("audio subsystem must be configured", NativeVideoEncoderMethods.GetLastErrorMessage());
            Assert.False(IsAudioEncoderConfigured());
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldConfigureAacEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());

            var result = NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.True(IsAudioEncoderConfigured());

            var info = NativeVideoEncoderMethods.CreateInfo();
            Assert.NotEqual(0, NativeVideoEncoderMethods.GetSelectedAudioEncoder(ref info));
            Assert.Equal(0, info.IsHardware);
            Assert.False(string.IsNullOrEmpty(NativeVideoEncoderMethods.FromFixedBuffer(info.Id)));
            Assert.False(string.IsNullOrEmpty(NativeVideoEncoderMethods.FromFixedBuffer(info.Name)));
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldBeIdempotentForSameSettings()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            var config = NativeVideoEncoderMethods.CreateAudioEncoderConfig();

            Assert.Equal(NativeVideoEncoderResult.Ok, NativeVideoEncoderMethods.ConfigureAudioEncoder(config));
            Assert.Equal(NativeVideoEncoderResult.Ok, NativeVideoEncoderMethods.ConfigureAudioEncoder(config));
            Assert.True(IsAudioEncoderConfigured());
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldRejectDifferentValuesWhileAlreadyConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig(audioBitrate: 128));

            var result = NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig(audioBitrate: 192));

            Assert.Equal(NativeVideoEncoderResult.AudioEncoderAlreadyConfigured, result);
            Assert.Contains("already configured", NativeVideoEncoderMethods.GetLastErrorMessage());
            Assert.True(IsAudioEncoderConfigured());
        }

        // Proves the audio encoder never has to wait on the video encoder:
        // configuring only audio here, and only video in the test below,
        // are both meant to work in isolation.
        [StaFact]
        public void ConfigureAudioEncoderShouldNotRequireVideoEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());

            var result = NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.True(IsAudioEncoderConfigured());
            Assert.False(NativeVideoEncoderMethods.IsVideoEncoderConfigured() != 0);
        }

        // The flip side: configuring the video encoder must still work
        // without ever configuring audio, which is what regresses if audio
        // encoder creation were ever folded into ConfigureVideoEncoder
        // instead of staying a separate entry point.
        [StaFact]
        public void ConfigureVideoEncoderShouldNotRequireAudioEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode));

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.True(NativeVideoEncoderMethods.IsVideoEncoderConfigured() != 0);
            Assert.False(IsAudioEncoderConfigured());
        }

        [Fact]
        public void GetSelectedAudioEncoderShouldFailWhenNotConfigured()
        {
            var info = NativeVideoEncoderMethods.CreateInfo();

            Assert.Equal(0, NativeVideoEncoderMethods.GetSelectedAudioEncoder(ref info));
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldWorkAfterRuntimeRestart()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var audioConfiguration = CreateAudioConfiguration();
            var encoderConfiguration = NativeVideoEncoderMethods.CreateAudioEncoderConfig();

            EngineRuntime.Initialize(runtimeConfiguration);
            EngineRuntime.ConfigureAudio(audioConfiguration);
            NativeVideoEncoderMethods.ConfigureAudioEncoder(encoderConfiguration);
            EngineRuntime.Shutdown();

            Assert.False(IsAudioEncoderConfigured());

            EngineRuntime.Initialize(runtimeConfiguration);
            EngineRuntime.ConfigureAudio(audioConfiguration);
            var result = NativeVideoEncoderMethods.ConfigureAudioEncoder(encoderConfiguration);

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.True(IsAudioEncoderConfigured());
        }

        [Fact(Skip =
            "castor_engine_configure_audio_encoder's OBS-rejection path " +
            "(CASTOR_ENGINE_AUDIO_ENCODER_CREATION_FAILED) cannot be reached " +
            "through the public API without a fault-injection seam in OBS: " +
            "the AAC encoder id is discovered through enumeration, so it is " +
            "always a real, creatable encoder by construction. There is no " +
            "black-box way to force obs_audio_encoder_create to return null " +
            "for it.")]
        public void ConfigureAudioEncoderShouldPropagateObsCreationFailures()
        {
        }

        [StaFact]
        public void ShutdownShouldClearAudioEncoderState()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            NativeVideoEncoderMethods.ConfigureAudioEncoder(
                NativeVideoEncoderMethods.CreateAudioEncoderConfig());

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(IsAudioEncoderConfigured());
        }

        private static bool IsAudioEncoderConfigured()
        {
            return NativeVideoEncoderMethods.IsAudioEncoderConfigured() != 0;
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
