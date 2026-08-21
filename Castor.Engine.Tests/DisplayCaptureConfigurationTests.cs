using Castor.Engine.Tests.Interop;

namespace Castor.Engine.Tests
{
    public sealed class DisplayCaptureConfigurationTests : IDisposable
    {
        public DisplayCaptureConfigurationTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [Fact]
        public void ValidationShouldNotRequireInitialization()
        {
            var config = NativeDisplayMethods.CreateConfig("display-1");

            Assert.Equal(
                NativeDisplayResult.Ok,
                NativeDisplayMethods.ValidateDisplayCaptureConfig(config));
            Assert.False(EngineRuntime.IsInitialized);
        }

        [Fact]
        public void ValidationShouldRejectNullConfiguration()
        {
            var result = NativeDisplayMethods.ValidateDisplayCaptureConfigRaw(nint.Zero);

            Assert.Equal(NativeDisplayResult.DisplayInvalidConfiguration, result);
            Assert.Contains("must not be null", NativeDisplayMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidationShouldRejectUndersizedConfiguration()
        {
            var config = NativeDisplayMethods.CreateConfig("display-1");
            config.StructSize = 1;

            var result = NativeDisplayMethods.ValidateDisplayCaptureConfig(config);

            Assert.Equal(NativeDisplayResult.DisplayInvalidConfiguration, result);
            Assert.Contains("too small", NativeDisplayMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidationShouldRejectEmptyDisplayIdentifier()
        {
            var config = NativeDisplayMethods.CreateConfig(string.Empty);

            var result = NativeDisplayMethods.ValidateDisplayCaptureConfig(config);

            Assert.Equal(NativeDisplayResult.DisplayInvalidConfiguration, result);
            Assert.Contains("must not be empty", NativeDisplayMethods.GetLastErrorMessage());
        }

        [Fact]
        public void ValidationShouldRejectInvalidCursorValue()
        {
            var config = NativeDisplayMethods.CreateConfig("display-1");
            config.CaptureCursor = 2;

            var result = NativeDisplayMethods.ValidateDisplayCaptureConfig(config);

            Assert.Equal(NativeDisplayResult.DisplayInvalidConfiguration, result);
            Assert.Contains("either 0 or 1", NativeDisplayMethods.GetLastErrorMessage());
        }
    }
}
