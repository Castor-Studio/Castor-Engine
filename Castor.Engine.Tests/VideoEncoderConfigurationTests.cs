using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class VideoEncoderConfigurationTests : IDisposable
    {
        public VideoEncoderConfigurationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ValidateVideoEncoderConfigShouldNotRequireEngineInitialization()
        {
            Assert.False(EngineRuntime.IsInitialized);

            var config = NativeVideoEncoderMethods.CreateConfig();
            var result = NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.False(EngineRuntime.IsInitialized);
        }

        [Fact]
        public void ValidateVideoEncoderConfigShouldAcceptDefaultSoftwareForcedConfiguration()
        {
            var config = NativeVideoEncoderMethods.CreateConfig();

            Assert.Equal(
                NativeVideoEncoderResult.Ok,
                NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config));
        }

        [Theory]
        [InlineData(NativeVideoEncoderMethods.AutomaticSelectionMode)]
        [InlineData(NativeVideoEncoderMethods.HardwarePreferredSelectionMode)]
        [InlineData(NativeVideoEncoderMethods.SoftwareForcedSelectionMode)]
        public void ValidateVideoEncoderConfigShouldAcceptSupportedSelectionModes(uint selectionMode)
        {
            var config = NativeVideoEncoderMethods.CreateConfig(selectionMode: selectionMode);

            Assert.Equal(
                NativeVideoEncoderResult.Ok,
                NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config));
        }

        [Theory]
        [InlineData(NativeVideoEncoderMethods.ConstantBitrateRateControl)]
        [InlineData(NativeVideoEncoderMethods.VariableBitrateRateControl)]
        [InlineData(NativeVideoEncoderMethods.ConstantQpRateControl)]
        [InlineData(NativeVideoEncoderMethods.ConstantRateFactorRateControl)]
        public void ValidateVideoEncoderConfigShouldAcceptSupportedRateControlModes(uint rateControl)
        {
            var config = NativeVideoEncoderMethods.CreateConfig(rateControl: rateControl);

            Assert.Equal(
                NativeVideoEncoderResult.Ok,
                NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config));
        }

        [Fact]
        public void ValidateVideoEncoderConfigShouldRejectNullPointer()
        {
            var result = NativeVideoEncoderMethods.ValidateVideoEncoderConfigRaw(nint.Zero);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("must not be null", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateVideoEncoderConfigShouldRejectUndersizedStructSize()
        {
            var config = NativeVideoEncoderMethods.CreateConfig();
            config.StructSize = 1;

            var result = NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("too small", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateVideoEncoderConfigShouldRejectUnsupportedSelectionMode()
        {
            var config = NativeVideoEncoderMethods.CreateConfig(selectionMode: 99);

            var result = NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("selection mode", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateVideoEncoderConfigShouldRejectUnsupportedRateControl()
        {
            var config = NativeVideoEncoderMethods.CreateConfig(rateControl: 99);

            var result = NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("rate control", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateVideoEncoderConfigShouldRejectZeroBitrate()
        {
            var config = NativeVideoEncoderMethods.CreateConfig(bitrate: 0);

            var result = NativeVideoEncoderMethods.ValidateVideoEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("bitrate", NativeVideoEncoderMethods.GetLastErrorMessage());
        }
    }
}
