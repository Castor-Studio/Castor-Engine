using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class AudioConfigurationTests : IDisposable
    {
        public AudioConfigurationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ValidateAudioConfigShouldNotRequireEngineInitialization()
        {
            Assert.False(EngineRuntime.IsInitialized);

            var config = NativeAudioMethods.CreateConfig(
                48000,
                NativeAudioMethods.StereoSpeakerLayout);
            var result = NativeAudioMethods.ValidateAudioConfig(config);

            Assert.Equal(NativeAudioResult.Ok, result);
            Assert.False(EngineRuntime.IsInitialized);
        }

        [Fact]
        public void ValidateAudioConfigShouldAcceptDefaultStereoConfiguration()
        {
            var config = NativeAudioMethods.CreateConfig(
                48000,
                NativeAudioMethods.StereoSpeakerLayout);

            Assert.Equal(
                NativeAudioResult.Ok,
                NativeAudioMethods.ValidateAudioConfig(config));
        }

        [Fact]
        public void ValidateAudioConfigShouldResolveZeroValuesToDefaults()
        {
            var config = NativeAudioMethods.CreateConfig(
                sampleRate: 0,
                speakerLayout: NativeAudioMethods.DefaultSpeakerLayout);

            Assert.Equal(
                NativeAudioResult.Ok,
                NativeAudioMethods.ValidateAudioConfig(config));
        }

        [Theory]
        [InlineData(44100u)]
        [InlineData(48000u)]
        public void ValidateAudioConfigShouldAcceptSupportedSampleRates(uint sampleRate)
        {
            var config = NativeAudioMethods.CreateConfig(
                sampleRate,
                NativeAudioMethods.StereoSpeakerLayout);

            Assert.Equal(
                NativeAudioResult.Ok,
                NativeAudioMethods.ValidateAudioConfig(config));
        }

        [Theory]
        [InlineData(NativeAudioMethods.MonoSpeakerLayout)]
        [InlineData(NativeAudioMethods.StereoSpeakerLayout)]
        public void ValidateAudioConfigShouldAcceptSupportedSpeakerLayouts(uint speakerLayout)
        {
            var config = NativeAudioMethods.CreateConfig(48000, speakerLayout);

            Assert.Equal(
                NativeAudioResult.Ok,
                NativeAudioMethods.ValidateAudioConfig(config));
        }

        [Fact]
        public void ValidateAudioConfigShouldRejectNullPointer()
        {
            var result = NativeAudioMethods.ValidateAudioConfigRaw(nint.Zero);

            Assert.Equal(NativeAudioResult.InvalidArgument, result);
            Assert.Contains("must not be null", NativeAudioMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateAudioConfigShouldRejectUndersizedStructSize()
        {
            var config = NativeAudioMethods.CreateConfig(
                48000,
                NativeAudioMethods.StereoSpeakerLayout);
            config.StructSize = 1;

            var result = NativeAudioMethods.ValidateAudioConfig(config);

            Assert.Equal(NativeAudioResult.InvalidArgument, result);
            Assert.Contains("too small", NativeAudioMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateAudioConfigShouldRejectUnsupportedSampleRate()
        {
            var config = NativeAudioMethods.CreateConfig(
                96000,
                NativeAudioMethods.StereoSpeakerLayout);

            var result = NativeAudioMethods.ValidateAudioConfig(config);

            Assert.Equal(NativeAudioResult.AudioUnsupportedSampleRate, result);
            Assert.Contains("sample rate", NativeAudioMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateAudioConfigShouldRejectUnsupportedSpeakerLayout()
        {
            var config = NativeAudioMethods.CreateConfig(48000, 99);

            var result = NativeAudioMethods.ValidateAudioConfig(config);

            Assert.Equal(NativeAudioResult.AudioUnsupportedSpeakerLayout, result);
            Assert.Contains("speaker layout", NativeAudioMethods.GetLastErrorMessage());
        }
    }
}
