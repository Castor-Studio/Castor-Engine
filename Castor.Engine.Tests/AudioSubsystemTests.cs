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
