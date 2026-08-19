using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class AudioEncoderConfigurationTests : IDisposable
    {
        public AudioEncoderConfigurationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ValidateAudioEncoderConfigShouldNotRequireEngineInitialization()
        {
            Assert.False(EngineRuntime.IsInitialized);

            var config = NativeVideoEncoderMethods.CreateAudioEncoderConfig();
            var result = NativeVideoEncoderMethods.ValidateAudioEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.Ok, result);
            Assert.False(EngineRuntime.IsInitialized);
        }

        [Fact]
        public void ValidateAudioEncoderConfigShouldAcceptDefaultConfiguration()
        {
            var config = NativeVideoEncoderMethods.CreateAudioEncoderConfig();

            Assert.Equal(
                NativeVideoEncoderResult.Ok,
                NativeVideoEncoderMethods.ValidateAudioEncoderConfig(config));
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(5u)]
        public void ValidateAudioEncoderConfigShouldAcceptEveryValidTrackIndex(uint audioTrackIndex)
        {
            var config = NativeVideoEncoderMethods.CreateAudioEncoderConfig(audioTrackIndex: audioTrackIndex);

            Assert.Equal(
                NativeVideoEncoderResult.Ok,
                NativeVideoEncoderMethods.ValidateAudioEncoderConfig(config));
        }

        [Fact]
        public void ValidateAudioEncoderConfigShouldRejectNullPointer()
        {
            var result = NativeVideoEncoderMethods.ValidateAudioEncoderConfigRaw(nint.Zero);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("must not be null", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateAudioEncoderConfigShouldRejectUndersizedStructSize()
        {
            var config = NativeVideoEncoderMethods.CreateAudioEncoderConfig();
            config.StructSize = 1;

            var result = NativeVideoEncoderMethods.ValidateAudioEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("too small", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateAudioEncoderConfigShouldRejectZeroBitrate()
        {
            var config = NativeVideoEncoderMethods.CreateAudioEncoderConfig(audioBitrate: 0);

            var result = NativeVideoEncoderMethods.ValidateAudioEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("bitrate", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateAudioEncoderConfigShouldRejectOutOfRangeTrackIndex()
        {
            var config = NativeVideoEncoderMethods.CreateAudioEncoderConfig(audioTrackIndex: 6);

            var result = NativeVideoEncoderMethods.ValidateAudioEncoderConfig(config);

            Assert.Equal(NativeVideoEncoderResult.InvalidArgument, result);
            Assert.Contains("track index", NativeVideoEncoderMethods.GetLastErrorMessage());
        }
    }
}
