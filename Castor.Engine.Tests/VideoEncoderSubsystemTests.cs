using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class VideoEncoderSubsystemTests : IDisposable
    {
        public VideoEncoderSubsystemTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ConfigureVideoEncoderShouldRequireInitialization()
        {
            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig());

            Assert.Equal(NativeVideoEncoderResult.NotInitialized, result);
            Assert.Contains("must be initialized", NativeVideoEncoderMethods.GetLastErrorMessage());
            Assert.False(IsVideoEncoderConfigured());
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldRequireVideoConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig());

            Assert.Equal(NativeVideoEncoderResult.VideoNotConfigured, result);
            Assert.Contains("video subsystem must be configured", NativeVideoEncoderMethods.GetLastErrorMessage());
            Assert.False(IsVideoEncoderConfigured());
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldConfigureForcedSoftwareEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode));

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.True(IsVideoEncoderConfigured());

            var info = NativeVideoEncoderMethods.CreateInfo();
            Assert.NotEqual(0, NativeVideoEncoderMethods.GetSelectedVideoEncoder(ref info));
            Assert.Equal(0, info.IsHardware);
            Assert.Equal(string.Empty, NativeVideoEncoderMethods.GetVideoEncoderFallbackNoticeMessage());
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldBeIdempotentForSameSettings()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            var config = NativeVideoEncoderMethods.CreateConfig(
                selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode);

            Assert.Equal(NativeVideoEncoderResult.Ok, NativeVideoEncoderMethods.ConfigureVideoEncoder(config));
            Assert.Equal(NativeVideoEncoderResult.Ok, NativeVideoEncoderMethods.ConfigureVideoEncoder(config));
            Assert.True(IsVideoEncoderConfigured());
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldRejectDifferentValuesWhileAlreadyConfigured()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode,
                    bitrate: 2500));

            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode,
                    bitrate: 4000));

            Assert.Equal(NativeVideoEncoderResult.VideoEncoderAlreadyConfigured, result);
            Assert.Contains("already configured", NativeVideoEncoderMethods.GetLastErrorMessage());
            Assert.True(IsVideoEncoderConfigured());
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldRejectUnknownExplicitEncoderId()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(encoderId: "not_a_real_encoder_id"));

            Assert.Equal(NativeVideoEncoderResult.VideoEncoderUnknownId, result);
            Assert.Contains("not_a_real_encoder_id", NativeVideoEncoderMethods.GetLastErrorMessage());
            Assert.False(IsVideoEncoderConfigured());
        }

        // Whether hardware encoding is available depends entirely on the
        // machine running the test - this asserts the behavior that must
        // hold in either case rather than requiring one specific outcome,
        // so it exercises real hardware when present without ever
        // false-failing on a machine that has none.
        [StaFact]
        public void ConfigureVideoEncoderShouldPreferHardwareWhenAvailableOtherwiseFallBackToSoftware()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            var hardwareEncoderAvailable = AnyHardwareVideoEncoderEnumerated();

            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.HardwarePreferredSelectionMode));

            Assert.Equal(NativeVideoEncoderResult.Ok, result);

            var info = NativeVideoEncoderMethods.CreateInfo();
            Assert.NotEqual(0, NativeVideoEncoderMethods.GetSelectedVideoEncoder(ref info));
            var fallbackNotice = NativeVideoEncoderMethods.GetVideoEncoderFallbackNoticeMessage();

            if (hardwareEncoderAvailable)
            {
                Assert.NotEqual(0, info.IsHardware);
                Assert.Equal(string.Empty, fallbackNotice);
            }
            else
            {
                Assert.Equal(0, info.IsHardware);
                Assert.NotEqual(string.Empty, fallbackNotice);
            }
        }

        [StaFact]
        public void GetVideoEncoderConfigShouldReturnEffectiveConfiguration()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode,
                    bitrate: 3000));

            var outConfig = NativeVideoEncoderMethods.CreateConfig();
            var succeeded = NativeVideoEncoderMethods.GetVideoEncoderConfig(ref outConfig) != 0;

            Assert.True(succeeded);
            Assert.Equal(3000u, outConfig.Bitrate);
        }

        [Fact]
        public void GetVideoEncoderConfigShouldFailWhenNotConfigured()
        {
            var outConfig = NativeVideoEncoderMethods.CreateConfig();

            Assert.Equal(0, NativeVideoEncoderMethods.GetVideoEncoderConfig(ref outConfig));
        }

        [Fact]
        public void GetSelectedVideoEncoderShouldFailWhenNotConfigured()
        {
            var info = NativeVideoEncoderMethods.CreateInfo();

            Assert.Equal(0, NativeVideoEncoderMethods.GetSelectedVideoEncoder(ref info));
        }

        [StaFact]
        public void ConfigureVideoEncoderShouldWorkAfterRuntimeRestart()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var videoConfiguration = CreateVideoConfiguration();
            var encoderConfiguration = NativeVideoEncoderMethods.CreateConfig(
                selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode);

            EngineRuntime.Initialize(runtimeConfiguration);
            EngineRuntime.ConfigureVideo(videoConfiguration);
            NativeVideoEncoderMethods.ConfigureVideoEncoder(encoderConfiguration);
            EngineRuntime.Shutdown();

            Assert.False(IsVideoEncoderConfigured());

            EngineRuntime.Initialize(runtimeConfiguration);
            EngineRuntime.ConfigureVideo(videoConfiguration);
            var result = NativeVideoEncoderMethods.ConfigureVideoEncoder(encoderConfiguration);

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.True(IsVideoEncoderConfigured());
        }

        [Fact(Skip =
            "castor_engine_configure_video_encoder's OBS-rejection path " +
            "(CASTOR_ENGINE_VIDEO_ENCODER_CREATION_FAILED) cannot be reached " +
            "through the public API without a fault-injection seam in OBS: " +
            "every id this test could pass either does not exist (caught " +
            "earlier as VideoEncoderUnknownId) or is a real, creatable " +
            "encoder. There is no black-box way to force obs_video_encoder_" +
            "create to return null for a valid, available id.")]
        public void ConfigureVideoEncoderShouldPropagateObsCreationFailures()
        {
        }

        [StaFact]
        public void ShutdownShouldClearVideoEncoderState()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());
            NativeVideoEncoderMethods.ConfigureVideoEncoder(
                NativeVideoEncoderMethods.CreateConfig(
                    selectionMode: NativeVideoEncoderMethods.SoftwareForcedSelectionMode));

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(IsVideoEncoderConfigured());
        }

        private static bool IsVideoEncoderConfigured()
        {
            return NativeVideoEncoderMethods.IsVideoEncoderConfigured() != 0;
        }

        private static bool AnyHardwareVideoEncoderEnumerated()
        {
            var count = NativeVideoEncoderMethods.GetVideoEncoderCount();

            for (uint index = 0; index < count; ++index)
            {
                var info = NativeVideoEncoderMethods.CreateInfo();

                if (NativeVideoEncoderMethods.GetVideoEncoderAt(index, ref info) != 0 && info.IsHardware != 0)
                {
                    return true;
                }
            }

            return false;
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
