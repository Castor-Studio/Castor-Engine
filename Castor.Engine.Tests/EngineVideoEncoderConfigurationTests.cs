using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class EngineVideoEncoderConfigurationTests : IDisposable
    {
        public EngineVideoEncoderConfigurationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldConfigureForcedSoftwareEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            EngineRuntime.ConfigureVideoEncoder(
                new EngineVideoEncoderConfiguration(
                    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced));

            Assert.True(EngineRuntime.IsVideoEncoderConfigured);

            var selected = EngineRuntime.GetSelectedVideoEncoder();
            Assert.False(selected.IsHardware);
            Assert.Equal(string.Empty, EngineRuntime.VideoEncoderFallbackNotice);
        }

        [Fact]
        public void ConfigureVideoEncoderShouldRejectNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(
                () => EngineRuntime.ConfigureVideoEncoder(null!));
        }

        [Fact]
        public void ConfigureVideoEncoderShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureVideoEncoder(new EngineVideoEncoderConfiguration()));

            Assert.Contains("NotInitialized", exception.Message);
            Assert.Contains("must be initialized", exception.Message);
            Assert.False(EngineRuntime.IsVideoEncoderConfigured);
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldRequireVideoConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureVideoEncoder(new EngineVideoEncoderConfiguration()));

            Assert.Contains("VideoNotConfigured", exception.Message);
            Assert.Contains("video subsystem must be configured", exception.Message);
            Assert.False(EngineRuntime.IsVideoEncoderConfigured);
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldRejectUnknownExplicitEncoderId()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureVideoEncoder(
                    new EngineVideoEncoderConfiguration(encoderId: "not_a_real_encoder_id")));

            Assert.Contains("VideoEncoderUnknownId", exception.Message);
            Assert.False(EngineRuntime.IsVideoEncoderConfigured);
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldBeIdempotentForSameSettings()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            var configuration = new EngineVideoEncoderConfiguration(
                selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced);

            EngineRuntime.ConfigureVideoEncoder(configuration);
            EngineRuntime.ConfigureVideoEncoder(configuration);

            Assert.True(EngineRuntime.IsVideoEncoderConfigured);
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldRejectDifferentValuesWhileAlreadyConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureVideoEncoder(
                new EngineVideoEncoderConfiguration(
                    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced,
                    bitrate: 2500));

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureVideoEncoder(
                    new EngineVideoEncoderConfiguration(
                        selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced,
                        bitrate: 4000)));

            Assert.Contains("VideoEncoderAlreadyConfigured", exception.Message);
            Assert.Contains("already configured", exception.Message);
            Assert.True(EngineRuntime.IsVideoEncoderConfigured);
        }

        [Fact]
        public void GetVideoEncoderConfigurationShouldRequireConfiguredEncoder()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                EngineRuntime.GetVideoEncoderConfiguration);

            Assert.Contains("not configured", exception.Message);
        }

        [Fact]
        public void GetSelectedVideoEncoderShouldRequireConfiguredEncoder()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                EngineRuntime.GetSelectedVideoEncoder);

            Assert.Contains("not configured", exception.Message);
        }

        [StaFact]
        public void EnumerateVideoEncodersShouldIncludeAtLeastTheSoftwareEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var encoders = EngineRuntime.EnumerateVideoEncoders();

            Assert.NotEmpty(encoders);
            Assert.Contains(encoders, encoder => !encoder.IsHardware);
            Assert.All(encoders, encoder =>
            {
                Assert.False(string.IsNullOrEmpty(encoder.Id));
                Assert.False(string.IsNullOrEmpty(encoder.Name));
            });
        }

        [StaFact]
        public void ShutdownShouldClearVideoEncoderState()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureVideoEncoder(
                new EngineVideoEncoderConfiguration(
                    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced));

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsVideoEncoderConfigured);
        }

        [StaFact]
        public void ManagedAndNativeEffectiveConfigurationShouldStayConsistent()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            EngineRuntime.ConfigureVideoEncoder(
                new EngineVideoEncoderConfiguration(
                    selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced,
                    bitrate: 3000));

            var managedConfiguration = EngineRuntime.GetVideoEncoderConfiguration();

            var nativeConfiguration = NativeVideoEncoderMethods.CreateConfig();
            var succeeded = NativeVideoEncoderMethods.GetVideoEncoderConfig(ref nativeConfiguration) != 0;

            Assert.True(succeeded);
            Assert.Equal(nativeConfiguration.Bitrate, managedConfiguration.Bitrate);
            Assert.Equal(
                nativeConfiguration.SelectionMode,
                (uint)managedConfiguration.SelectionMode);
        }

        [StaFact]
        public void VideoEncoderLifecycleShouldRunEndToEndAndRepeat()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var videoConfiguration = CreateVideoConfiguration();
            var encoderConfiguration = new EngineVideoEncoderConfiguration(
                selectionMode: EngineVideoEncoderSelectionMode.SoftwareForced);

            RunLifecycle();
            RunLifecycle();

            void RunLifecycle()
            {
                // 1. Initialize the engine.
                EngineRuntime.Initialize(runtimeConfiguration);

                // 2. Configure the video subsystem.
                EngineRuntime.ConfigureVideo(videoConfiguration);

                // 3. Configure the forced-software video encoder.
                EngineRuntime.ConfigureVideoEncoder(encoderConfiguration);

                // 4. Verify an active configuration is reported.
                Assert.True(EngineRuntime.IsVideoEncoderConfigured);

                // 5. Verify the selected encoder is the software encoder.
                Assert.False(EngineRuntime.GetSelectedVideoEncoder().IsHardware);

                // 6. Shut down the engine.
                EngineRuntime.Shutdown();

                // 7. Verify the engine video encoder state is cleared.
                Assert.False(EngineRuntime.IsVideoEncoderConfigured);

                // 8. The caller repeats the lifecycle by calling RunLifecycle() again.
            }
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
