using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class AudioSubsystemTests : IDisposable
    {
        public AudioSubsystemTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [StaFact]
        public void ConfigureAudioShouldConfigureDefaultStereoSubsystem()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var result = NativeAudioMethods.ConfigureAudio(
                NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.StereoSpeakerLayout));

            Assert.Equal(NativeAudioResult.Ok, result);
            Assert.True(IsAudioConfigured());
        }

        [Fact]
        public void ConfigureAudioShouldRequireInitialization()
        {
            var result = NativeAudioMethods.ConfigureAudio(
                NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.StereoSpeakerLayout));

            Assert.Equal(NativeAudioResult.NotInitialized, result);
            Assert.Contains("must be initialized", NativeAudioMethods.GetLastErrorMessage());
            Assert.False(IsAudioConfigured());
        }

        [StaFact]
        public void ConfigureAudioShouldBeIdempotentForSameSettings()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var config = NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.StereoSpeakerLayout);

            Assert.Equal(NativeAudioResult.Ok, NativeAudioMethods.ConfigureAudio(config));
            Assert.Equal(NativeAudioResult.Ok, NativeAudioMethods.ConfigureAudio(config));
            Assert.True(IsAudioConfigured());
        }

        [StaFact]
        public void ConfigureAudioShouldTreatZeroValuedFieldsAsMatchingResolvedDefaults()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            Assert.Equal(
                NativeAudioResult.Ok,
                NativeAudioMethods.ConfigureAudio(
                    NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.StereoSpeakerLayout)));
            Assert.Equal(
                NativeAudioResult.Ok,
                NativeAudioMethods.ConfigureAudio(NativeAudioMethods.CreateConfig(0, 0)));
            Assert.True(IsAudioConfigured());
        }

        [StaFact]
        public void ConfigureAudioShouldRejectDifferentValuesWhileAlreadyConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            NativeAudioMethods.ConfigureAudio(
                NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.StereoSpeakerLayout));

            var result = NativeAudioMethods.ConfigureAudio(
                NativeAudioMethods.CreateConfig(44100, NativeAudioMethods.StereoSpeakerLayout));

            Assert.Equal(NativeAudioResult.AudioAlreadyConfigured, result);
            Assert.Contains("already configured", NativeAudioMethods.GetLastErrorMessage());
            Assert.True(IsAudioConfigured());
        }

        [StaFact]
        public void ConfigureAudioShouldRejectReconfigurationWhileRecordingIsActive()
        {
            // Castor Engine does not track recording state yet (see issue #14).
            // OBS itself has no runtime audio reconfiguration path at all:
            // obs_reset_audio() silently keeps the existing settings once
            // configured, whether or not a recording is active. Rejecting
            // every differing reconfiguration unconditionally is what makes
            // this safe for an active recording - there is no state in which
            // OBS would actually swap settings out from under one.
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            NativeAudioMethods.ConfigureAudio(
                NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.StereoSpeakerLayout));

            var result = NativeAudioMethods.ConfigureAudio(
                NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.MonoSpeakerLayout));

            Assert.Equal(NativeAudioResult.AudioAlreadyConfigured, result);
            Assert.True(IsAudioConfigured());
        }

        [StaFact]
        public void GetAudioConfigShouldReturnEffectiveConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            NativeAudioMethods.ConfigureAudio(NativeAudioMethods.CreateConfig(0, 0));

            var outConfig = NativeAudioMethods.CreateConfig();
            var succeeded = NativeAudioMethods.GetAudioConfig(ref outConfig) != 0;

            Assert.True(succeeded);
            Assert.Equal(48000u, outConfig.SampleRate);
            Assert.Equal(NativeAudioMethods.StereoSpeakerLayout, outConfig.SpeakerLayout);
        }

        [Fact]
        public void GetAudioConfigShouldFailWhenNotConfigured()
        {
            var outConfig = NativeAudioMethods.CreateConfig();

            var succeeded = NativeAudioMethods.GetAudioConfig(ref outConfig) != 0;

            Assert.False(succeeded);
        }

        [StaFact]
        public void ConfigureAudioShouldWorkAfterRuntimeRestart()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var audioConfiguration = NativeAudioMethods.CreateConfig(
                48000,
                NativeAudioMethods.StereoSpeakerLayout);

            EngineRuntime.Initialize(runtimeConfiguration);
            NativeAudioMethods.ConfigureAudio(audioConfiguration);
            EngineRuntime.Shutdown();

            Assert.False(IsAudioConfigured());

            EngineRuntime.Initialize(runtimeConfiguration);
            var result = NativeAudioMethods.ConfigureAudio(audioConfiguration);

            Assert.Equal(NativeAudioResult.Ok, result);
            Assert.True(IsAudioConfigured());
        }

        [Fact(Skip =
            "castor_engine_configure_audio's OBS-rejection path " +
            "(CASTOR_ENGINE_AUDIO_CONFIGURATION_FAILED) cannot be reached " +
            "through the public API without a physical device or a fault-" +
            "injection seam in OBS. obs_reset_audio only fails when OBS " +
            "itself isn't running, which is already surfaced separately as " +
            "CASTOR_ENGINE_NOT_INITIALIZED before OBS is ever called, or " +
            "when passed a null pointer, which castor_engine_configure_audio " +
            "never does. There is no black-box way to force libobs's " +
            "internal audio_output_open to fail.")]
        public void ConfigureAudioShouldPropagateObsInitializationFailures()
        {
        }

        [StaFact]
        public void ShutdownShouldClearAudioState()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            NativeAudioMethods.ConfigureAudio(
                NativeAudioMethods.CreateConfig(48000, NativeAudioMethods.StereoSpeakerLayout));

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(IsAudioConfigured());
        }

        private static bool IsAudioConfigured()
        {
            return NativeAudioMethods.IsAudioConfigured() != 0;
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }
    }
}
