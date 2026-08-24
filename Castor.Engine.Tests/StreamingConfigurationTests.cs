using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class StreamingConfigurationTests : IDisposable
    {
        public StreamingConfigurationTests() => EngineRuntime.Shutdown();
        public void Dispose() => EngineRuntime.Shutdown();

        [Fact]
        public void ValidationShouldNotRequireInitialization()
        {
            var config = NativeStreamingMethods.Create();
            try
            {
                Assert.Equal(NativeStreamingResult.Ok, NativeStreamingMethods.Validate(config));
                Assert.False(EngineRuntime.IsInitialized);
            }
            finally
            {
                NativeStreamingMethods.Free(config);
            }
        }

        [Fact]
        public void ValidationShouldRejectNullAndUndersizedConfiguration()
        {
            Assert.Equal(NativeStreamingResult.StreamingInvalidConfiguration,
                NativeStreamingMethods.ValidateRaw(nint.Zero));
            var config = NativeStreamingMethods.Create();
            config.StructSize = 1;
            try
            {
                Assert.Equal(NativeStreamingResult.StreamingInvalidConfiguration,
                    NativeStreamingMethods.Validate(config));
            }
            finally
            {
                NativeStreamingMethods.Free(config);
            }
        }

        [Theory]
        [InlineData("https://example.test/live", "key", false, "", "", 20, 2)]
        [InlineData("rtmp://", "key", false, "", "", 20, 2)]
        [InlineData("rtmp://localhost/live", "", false, "", "", 20, 2)]
        [InlineData("rtmp://localhost/live", "key", true, "user", "", 20, 2)]
        [InlineData("rtmp://localhost/live", "key", false, "", "", 1, 0)]
        public void ValidationShouldRejectInvalidValues(
            string server, string key, bool auth, string username, string password, uint retries, uint delay)
        {
            var config = NativeStreamingMethods.Create(server, key, auth, username, password, retries, delay);
            try
            {
                Assert.Equal(NativeStreamingResult.StreamingInvalidConfiguration,
                    NativeStreamingMethods.Validate(config));
            }
            finally
            {
                NativeStreamingMethods.Free(config);
            }
        }

        [Fact]
        public void ManagedConfigurationShouldUseDocumentedDefaults()
        {
            var config = new EngineStreamingConfiguration("rtmp://localhost/live", "key");
            Assert.Equal((uint)20, config.ReconnectRetryCount);
            Assert.Equal((uint)2, config.ReconnectDelaySeconds);
            Assert.False(config.UseAuthentication);
        }
    }
}
