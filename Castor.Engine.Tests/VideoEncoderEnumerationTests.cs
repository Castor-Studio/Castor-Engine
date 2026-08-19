using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class VideoEncoderEnumerationTests : IDisposable
    {
        public VideoEncoderEnumerationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void GetVideoEncoderCountShouldBeZeroBeforeInitialization()
        {
            Assert.Equal(0u, NativeVideoEncoderMethods.GetVideoEncoderCount());
        }

        [Fact]
        public void GetVideoEncoderAtShouldFailBeforeInitialization()
        {
            var info = NativeVideoEncoderMethods.CreateInfo();

            var succeeded = NativeVideoEncoderMethods.GetVideoEncoderAt(0, ref info) != 0;

            Assert.False(succeeded);
            Assert.Contains("must be initialized", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [StaFact]
        public void GetVideoEncoderCountShouldIncludeAtLeastTheSoftwareEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            Assert.True(NativeVideoEncoderMethods.GetVideoEncoderCount() >= 1);
        }

        [StaFact]
        public void GetVideoEncoderAtShouldReturnNonEmptyMetadataForEveryEncoder()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var count = NativeVideoEncoderMethods.GetVideoEncoderCount();

            Assert.True(count >= 1);

            var sawSoftwareEncoder = false;

            for (uint index = 0; index < count; ++index)
            {
                var info = NativeVideoEncoderMethods.CreateInfo();
                var succeeded = NativeVideoEncoderMethods.GetVideoEncoderAt(index, ref info) != 0;

                Assert.True(succeeded);

                var id = NativeVideoEncoderMethods.FromFixedBuffer(info.Id);
                var name = NativeVideoEncoderMethods.FromFixedBuffer(info.Name);

                Assert.False(string.IsNullOrEmpty(id));
                Assert.False(string.IsNullOrEmpty(name));
                Assert.True(info.IsAvailable != 0);

                sawSoftwareEncoder |= info.IsHardware == 0;
            }

            Assert.True(sawSoftwareEncoder, "Expected the packaged software encoder to be enumerated.");
        }

        [StaFact]
        public void GetVideoEncoderAtShouldFailForOutOfRangeIndex()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var count = NativeVideoEncoderMethods.GetVideoEncoderCount();
            var info = NativeVideoEncoderMethods.CreateInfo();

            var succeeded = NativeVideoEncoderMethods.GetVideoEncoderAt(count, ref info) != 0;

            Assert.False(succeeded);
        }

        [Fact]
        public void GetVideoEncoderAtShouldRejectNullPointer()
        {
            var result = NativeVideoEncoderMethods.GetVideoEncoderAtRaw(0, nint.Zero);

            Assert.Equal(0, result);
            Assert.Contains("must not be null", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        [StaFact]
        public void GetVideoEncoderAtShouldRejectUndersizedStructSize()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var info = NativeVideoEncoderMethods.CreateInfo();
            info.StructSize = 1;

            var succeeded = NativeVideoEncoderMethods.GetVideoEncoderAt(0, ref info) != 0;

            Assert.False(succeeded);
            Assert.Contains("too small", NativeVideoEncoderMethods.GetLastErrorMessage());
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }
    }
}
