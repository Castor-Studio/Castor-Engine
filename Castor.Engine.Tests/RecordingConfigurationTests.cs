using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class RecordingConfigurationTests : IDisposable
    {
        public RecordingConfigurationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ValidateRecordingConfigShouldNotRequireEngineInitialization()
        {
            Assert.False(EngineRuntime.IsInitialized);
            var config = NativeRecordingMethods.CreateConfig("recording.mkv");

            try
            {
                var result = NativeRecordingMethods.ValidateRecordingConfig(config);

                Assert.Equal(NativeRecordingResult.Ok, result);
                Assert.False(EngineRuntime.IsInitialized);
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        [Fact]
        public void ValidateRecordingConfigShouldAcceptAValidDestinationPath()
        {
            var config = NativeRecordingMethods.CreateConfig(@"C:\recordings\output.mkv");

            try
            {
                Assert.Equal(
                    NativeRecordingResult.Ok,
                    NativeRecordingMethods.ValidateRecordingConfig(config));
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        [Fact]
        public void ValidateRecordingConfigShouldRejectNullPointer()
        {
            var result = NativeRecordingMethods.ValidateRecordingConfigRaw(nint.Zero);

            Assert.Equal(NativeRecordingResult.InvalidArgument, result);
            Assert.Contains("must not be null", NativeRecordingMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateRecordingConfigShouldRejectUndersizedStructSize()
        {
            var config = NativeRecordingMethods.CreateConfig("recording.mkv");
            config.StructSize = 1;

            try
            {
                var result = NativeRecordingMethods.ValidateRecordingConfig(config);

                Assert.Equal(NativeRecordingResult.InvalidArgument, result);
                Assert.Contains("too small", NativeRecordingMethods.GetLastErrorMessage());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }

        [Fact]
        public void ValidateRecordingConfigShouldRejectNullDestinationPath()
        {
            var config = NativeRecordingMethods.CreateConfig(null);

            var result = NativeRecordingMethods.ValidateRecordingConfig(config);

            Assert.Equal(NativeRecordingResult.InvalidArgument, result);
            Assert.Contains("must not be null or empty", NativeRecordingMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidateRecordingConfigShouldRejectEmptyDestinationPath()
        {
            var config = NativeRecordingMethods.CreateConfig(string.Empty);

            try
            {
                var result = NativeRecordingMethods.ValidateRecordingConfig(config);

                Assert.Equal(NativeRecordingResult.InvalidArgument, result);
                Assert.Contains("must not be null or empty", NativeRecordingMethods.GetLastErrorMessage());
            }
            finally
            {
                NativeRecordingMethods.FreeConfig(config);
            }
        }
    }
}
