using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class EngineAudioConfigurationTests : IDisposable
    {
        public EngineAudioConfigurationTests()
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

            EngineRuntime.ConfigureAudio(
                new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo));

            Assert.True(EngineRuntime.IsAudioConfigured);
        }

        [Fact]
        public void ConfigureAudioShouldRejectNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(
                () => EngineRuntime.ConfigureAudio(null!));
        }

        [Fact]
        public void ConfigureAudioShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudio(
                    new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo)));

            Assert.Contains("NotInitialized", exception.Message);
            Assert.Contains("must be initialized", exception.Message);
            Assert.False(EngineRuntime.IsAudioConfigured);
        }

        [StaFact]
        public void ConfigureAudioShouldRejectUnsupportedSampleRate()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudio(
                    new EngineAudioConfiguration(96000, EngineSpeakerLayout.Stereo)));

            Assert.Contains("AudioUnsupportedSampleRate", exception.Message);
            Assert.Contains("sample rate", exception.Message);
            Assert.False(EngineRuntime.IsAudioConfigured);
        }

        [StaFact]
        public void ConfigureAudioShouldRejectUnsupportedSpeakerLayout()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudio(
                    new EngineAudioConfiguration(48000, (EngineSpeakerLayout)99)));

            Assert.Contains("AudioUnsupportedSpeakerLayout", exception.Message);
            Assert.Contains("speaker layout", exception.Message);
            Assert.False(EngineRuntime.IsAudioConfigured);
        }

        [StaFact]
        public void ConfigureAudioShouldResolveDefaultsForZeroValuedConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            EngineRuntime.ConfigureAudio(new EngineAudioConfiguration());

            var effective = EngineRuntime.GetAudioConfiguration();
            Assert.Equal(48000u, effective.SampleRate);
            Assert.Equal(EngineSpeakerLayout.Stereo, effective.SpeakerLayout);
        }

        [StaFact]
        public void ConfigureAudioShouldBeIdempotentForSameSettings()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var configuration = new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo);

            EngineRuntime.ConfigureAudio(configuration);
            EngineRuntime.ConfigureAudio(configuration);

            Assert.True(EngineRuntime.IsAudioConfigured);
        }

        [StaFact]
        public void ConfigureAudioShouldRejectDifferentValuesWhileAlreadyConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(
                new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo));

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudio(
                    new EngineAudioConfiguration(44100, EngineSpeakerLayout.Stereo)));

            Assert.Contains("AudioAlreadyConfigured", exception.Message);
            Assert.Contains("already configured", exception.Message);
            Assert.True(EngineRuntime.IsAudioConfigured);
        }

        [Fact]
        public void GetAudioConfigurationShouldRequireConfiguredAudio()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                EngineRuntime.GetAudioConfiguration);

            Assert.Contains("not configured", exception.Message);
        }

        [StaFact]
        public void ShutdownShouldClearAudioState()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(
                new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo));

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsAudioConfigured);
        }

        [StaFact]
        public void ManagedAndNativeEffectiveConfigurationShouldStayConsistent()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(
                new EngineAudioConfiguration(44100, EngineSpeakerLayout.Mono));

            var managedConfiguration = EngineRuntime.GetAudioConfiguration();

            var nativeConfiguration = NativeAudioMethods.CreateConfig();
            var succeeded = NativeAudioMethods.GetAudioConfig(ref nativeConfiguration) != 0;

            Assert.True(succeeded);
            Assert.Equal(nativeConfiguration.SampleRate, managedConfiguration.SampleRate);
            Assert.Equal(
                nativeConfiguration.SpeakerLayout,
                (uint)managedConfiguration.SpeakerLayout);
        }

        [StaFact]
        public void AudioLifecycleShouldRunEndToEndAndRepeat()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var audioConfiguration = new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo);

            RunLifecycle();
            RunLifecycle();

            void RunLifecycle()
            {
                // 1. Initialize the engine.
                EngineRuntime.Initialize(runtimeConfiguration);

                // 2. Configure 48 kHz stereo audio.
                EngineRuntime.ConfigureAudio(audioConfiguration);

                // 3. Verify an active configuration is reported.
                Assert.True(EngineRuntime.IsAudioConfigured);

                // 4. Verify the effective configuration matches the request.
                var effective = EngineRuntime.GetAudioConfiguration();
                Assert.Equal(48000u, effective.SampleRate);
                Assert.Equal(EngineSpeakerLayout.Stereo, effective.SpeakerLayout);

                // 5. Shut down the engine.
                EngineRuntime.Shutdown();

                // 6. Verify the engine audio state is cleared.
                Assert.False(EngineRuntime.IsAudioConfigured);

                // 7. The caller repeats the lifecycle by calling RunLifecycle() again.
            }
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }
    }
}
