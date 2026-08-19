using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class EngineAudioEncoderConfigurationTests : IDisposable
    {
        public EngineAudioEncoderConfigurationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldConfigureAacEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());

            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

            Assert.True(EngineRuntime.IsAudioEncoderConfigured);

            var selected = EngineRuntime.GetSelectedAudioEncoder();
            Assert.False(selected.IsHardware);
            Assert.False(string.IsNullOrEmpty(selected.Id));
            Assert.False(string.IsNullOrEmpty(selected.Name));
        }

        [Fact]
        public void ConfigureAudioEncoderShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0));

            Assert.Contains("NotInitialized", exception.Message);
            Assert.Contains("must be initialized", exception.Message);
            Assert.False(EngineRuntime.IsAudioEncoderConfigured);
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldRequireAudioConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0));

            Assert.Contains("AudioNotConfigured", exception.Message);
            Assert.Contains("audio subsystem must be configured", exception.Message);
            Assert.False(EngineRuntime.IsAudioEncoderConfigured);
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldRejectZeroBitrate()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudioEncoder(audioBitrate: 0, audioTrackIndex: 0));

            Assert.Contains("bitrate", exception.Message);
            Assert.False(EngineRuntime.IsAudioEncoderConfigured);
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldRejectOutOfRangeTrackIndex()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 6));

            Assert.Contains("track index", exception.Message);
            Assert.False(EngineRuntime.IsAudioEncoderConfigured);
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldBeIdempotentForSameSettings()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());

            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

            Assert.True(EngineRuntime.IsAudioEncoderConfigured);
        }

        [StaFact]
        public void ConfigureAudioEncoderShouldRejectDifferentValuesWhileAlreadyConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureAudioEncoder(audioBitrate: 192, audioTrackIndex: 0));

            Assert.Contains("AudioEncoderAlreadyConfigured", exception.Message);
            Assert.Contains("already configured", exception.Message);
            Assert.True(EngineRuntime.IsAudioEncoderConfigured);
        }

        [Fact]
        public void GetSelectedAudioEncoderShouldRequireConfiguredEncoder()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                EngineRuntime.GetSelectedAudioEncoder);

            Assert.Contains("not configured", exception.Message);
        }

        [StaFact]
        public void ShutdownShouldClearAudioEncoderState()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsAudioEncoderConfigured);
        }

        [StaFact]
        public void ManagedAndNativeSelectedEncoderShouldStayConsistent()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureAudio(CreateAudioConfiguration());
            EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

            var managedSelected = EngineRuntime.GetSelectedAudioEncoder();

            var nativeInfo = NativeVideoEncoderMethods.CreateInfo();
            var succeeded = NativeVideoEncoderMethods.GetSelectedAudioEncoder(ref nativeInfo) != 0;

            Assert.True(succeeded);
            Assert.Equal(NativeVideoEncoderMethods.FromFixedBuffer(nativeInfo.Id), managedSelected.Id);
            Assert.Equal(NativeVideoEncoderMethods.FromFixedBuffer(nativeInfo.Name), managedSelected.Name);
            Assert.Equal(nativeInfo.IsHardware != 0, managedSelected.IsHardware);
        }

        [StaFact]
        public void AudioEncoderLifecycleShouldRunEndToEndAndRepeat()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var audioConfiguration = CreateAudioConfiguration();

            RunLifecycle();
            RunLifecycle();

            void RunLifecycle()
            {
                // 1. Initialize the engine.
                EngineRuntime.Initialize(runtimeConfiguration);

                // 2. Configure the audio subsystem.
                EngineRuntime.ConfigureAudio(audioConfiguration);

                // 3. Configure the AAC audio encoder.
                EngineRuntime.ConfigureAudioEncoder(audioBitrate: 128, audioTrackIndex: 0);

                // 4. Verify an active configuration is reported.
                Assert.True(EngineRuntime.IsAudioEncoderConfigured);

                // 5. Verify the selected encoder is software (AAC has no
                // hardware implementation in this codebase).
                Assert.False(EngineRuntime.GetSelectedAudioEncoder().IsHardware);

                // 6. Shut down the engine.
                EngineRuntime.Shutdown();

                // 7. Verify the engine audio encoder state is cleared.
                Assert.False(EngineRuntime.IsAudioEncoderConfigured);

                // 8. The caller repeats the lifecycle by calling RunLifecycle() again.
            }
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }

        private static EngineAudioConfiguration CreateAudioConfiguration()
        {
            return new EngineAudioConfiguration(48000, EngineSpeakerLayout.Stereo);
        }
    }
}
