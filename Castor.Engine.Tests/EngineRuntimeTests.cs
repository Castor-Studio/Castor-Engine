namespace Castor.Engine.Tests
{
    public sealed class EngineRuntimeTests : IDisposable
    {
        public EngineRuntimeTests()
        {
            EngineRuntime.Shutdown();
        }

        public void Dispose()
        {
            EngineRuntime.Shutdown();
        }

        [StaFact]
        public void InitializeShouldInitializeRuntime()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            Assert.True(EngineRuntime.IsInitialized);
        }

        [StaFact]
        public void InitializeShouldBeIdempotent()
        {
            var configuration = CreateRuntimeConfiguration();

            EngineRuntime.Initialize(configuration);
            var firstModuleCount = EngineRuntime.LoadedModuleCount;

            EngineRuntime.Initialize(configuration);
            var secondModuleCount = EngineRuntime.LoadedModuleCount;

            Assert.True(EngineRuntime.IsInitialized);
            Assert.NotEqual(0U, firstModuleCount);
            Assert.Equal(firstModuleCount, secondModuleCount);
        }

        [StaFact]
        public void InitializeShouldLoadPackagedObsModule()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            Assert.NotEqual(0U, EngineRuntime.LoadedModuleCount);
            Assert.True(EngineRuntime.IsModuleLoaded("image-source"));
        }

        [StaFact]
        public void InitializeShouldRejectInvalidRuntimeRoot()
        {
            var invalidRuntimeRoot = Path.Combine(
                AppContext.BaseDirectory,
                "missing-runtime");
            var configuration = new EngineRuntimeConfiguration(
                invalidRuntimeRoot);

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.Initialize(configuration));

            Assert.Contains("InvalidRuntime", exception.Message);
            Assert.Contains("missing-runtime", exception.Message);
            Assert.False(EngineRuntime.IsInitialized);
        }

        [StaFact]
        public void ConfigureVideoShouldConfigurePackagedD3D11Runtime()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());

            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            Assert.True(EngineRuntime.IsVideoConfigured);
        }

        [StaFact]
        public void ConfigureVideoShouldBeIdempotentForSameSettings()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var configuration = CreateVideoConfiguration();

            EngineRuntime.ConfigureVideo(configuration);
            EngineRuntime.ConfigureVideo(configuration);

            Assert.True(EngineRuntime.IsVideoConfigured);
        }

        [StaFact]
        public void ConfigureVideoShouldRejectOddDimensions()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var configuration = new EngineVideoConfiguration(
                1280,
                720,
                1279,
                720,
                30,
                1);

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureVideo(configuration));

            Assert.Contains("InvalidArgument", exception.Message);
            Assert.Contains("output video width must be even", exception.Message);
            Assert.False(EngineRuntime.IsVideoConfigured);
        }

        [StaFact]
        public void ConfigureVideoShouldRejectZeroFps()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            var configuration = new EngineVideoConfiguration(
                1280,
                720,
                1280,
                720,
                0,
                1);

            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureVideo(configuration));

            Assert.Contains("InvalidArgument", exception.Message);
            Assert.Contains("must both be non-zero", exception.Message);
            Assert.False(EngineRuntime.IsVideoConfigured);
        }

        [Fact]
        public void ConfigureVideoShouldRequireInitialization()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => EngineRuntime.ConfigureVideo(
                    CreateVideoConfiguration()));

            Assert.Contains("NotInitialized", exception.Message);
            Assert.Contains("must be initialized", exception.Message);
            Assert.False(EngineRuntime.IsVideoConfigured);
        }

        [StaFact]
        public void ConfigureVideoShouldWorkAfterRuntimeRestart()
        {
            var runtimeConfiguration = CreateRuntimeConfiguration();
            var videoConfiguration = CreateVideoConfiguration();

            EngineRuntime.Initialize(runtimeConfiguration);
            EngineRuntime.ConfigureVideo(videoConfiguration);
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsVideoConfigured);

            EngineRuntime.Initialize(runtimeConfiguration);
            EngineRuntime.ConfigureVideo(videoConfiguration);

            Assert.True(EngineRuntime.IsVideoConfigured);
        }

        [StaFact]
        public void ShutdownShouldBeIdempotent()
        {
            EngineRuntime.Initialize(CreateRuntimeConfiguration());
            EngineRuntime.ConfigureVideo(CreateVideoConfiguration());

            EngineRuntime.Shutdown();
            EngineRuntime.Shutdown();

            Assert.False(EngineRuntime.IsInitialized);
            Assert.Equal(0U, EngineRuntime.LoadedModuleCount);
            Assert.False(EngineRuntime.IsVideoConfigured);
        }

        private static EngineRuntimeConfiguration CreateRuntimeConfiguration()
        {
            return new EngineRuntimeConfiguration(AppContext.BaseDirectory);
        }

        private static EngineVideoConfiguration CreateVideoConfiguration()
        {
            return new EngineVideoConfiguration(
                1280,
                720,
                1280,
                720,
                30,
                1);
        }
    }
}
